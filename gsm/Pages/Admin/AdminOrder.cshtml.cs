using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using gsm.Data;
using gsm.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages;

[Authorize(Roles = "Boss,Administrator,Technician")]
public class AdminOrderModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly AdbDiagnosticService _adbDiagnosticService;
    private readonly IWebHostEnvironment _environment;
    public AdminOrderModel(ApplicationDbContext dbContext, AdbDiagnosticService adbDiagnosticService, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _adbDiagnosticService = adbDiagnosticService;
        _environment = environment;
    }

    public ServiceOrder? Order { get; private set; }
    public List<WarehouseItem> WarehouseItems { get; private set; } = [];
    public List<ApplicationUser> Customers { get; private set; } = [];
    public List<CustomerDevice> CustomerDevices { get; private set; } = [];
    public OrdersModel.AdbDiagnosticDetails? AdbReport { get; private set; }

    [BindProperty] public OrderEditInput Input { get; set; } = new();
    [BindProperty] public List<PartInput> Parts { get; set; } = [];
    [BindProperty] public List<ServiceInput> Services { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        PopulateInputs();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveOrderAsync(int id)
    {
        var order = await _dbContext.ServiceOrders
            .Include(item => item.Lines)
            .Include(item => item.Adjustments)
            .Include(item => item.CustomerDevice!)
                .ThenInclude(device => device.Photos)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (order == null) return NotFound();

        if (Input.DevicePhotos.Count > 10)
            ModelState.AddModelError("Input.DevicePhotos", "You can upload up to 10 photos at a time.");
        foreach (var photo in Input.DevicePhotos)
        {
            if (photo.Length > 10 * 1024 * 1024 || !IsAllowedPhoto(photo))
                ModelState.AddModelError("Input.DevicePhotos", "Photos must be JPG, PNG, GIF or WEBP files up to 10 MB each.");
        }

        var customer = string.IsNullOrWhiteSpace(Input.CustomerId)
            ? null
            : await _dbContext.Users.FirstOrDefaultAsync(user => user.Id == Input.CustomerId && user.CompanyId == order.CompanyId);
        if (!string.IsNullOrWhiteSpace(Input.CustomerId) && customer == null)
            ModelState.AddModelError("Input.CustomerId", "Select a registered customer.");

        CustomerDevice? device = null;
        if (Input.CustomerDeviceId.HasValue)
        {
            device = await _dbContext.CustomerDevices.FirstOrDefaultAsync(item =>
                item.Id == Input.CustomerDeviceId && item.CustomerId == Input.CustomerId);
            if (device == null)
                ModelState.AddModelError("Input.CustomerDeviceId", "Select one of this customer's devices.");
        }
        else if (customer != null && !string.IsNullOrWhiteSpace(Input.NewDeviceType))
        {
            device = new CustomerDevice
            {
                CompanyId = order.CompanyId,
                CustomerId = customer.Id,
                DeviceType = Input.NewDeviceType.Trim(),
                ModelAndSerialNumber = Input.NewDeviceModelAndSerialNumber?.Trim()
            };
            _dbContext.CustomerDevices.Add(device);
        }

        var parts = Parts.Where(item => item.WarehouseItemId.HasValue && item.WarehouseItemId.Value > 0).ToList();
        var services = Services.Where(item => !string.IsNullOrWhiteSpace(item.Description)).ToList();
        if (parts.Any(item => item.Quantity <= 0 || item.UnitPrice < 0) || services.Any(item => item.UnitPrice < 0))
            ModelState.AddModelError(string.Empty, "Prices cannot be negative and part quantity must be at least 1.");

        var stock = await _dbContext.WarehouseItems.ToDictionaryAsync(item => item.Id);
        var affectedItemIds = order.Lines
            .Where(line => line.IsWarehousePart && line.WarehouseItemId.HasValue)
            .Select(line => line.WarehouseItemId!.Value)
            .Concat(parts.Select(item => item.WarehouseItemId!.Value))
            .ToHashSet();
        var quantitiesBefore = affectedItemIds
            .Where(stock.ContainsKey)
            .ToDictionary(itemId => itemId, itemId => stock[itemId].Quantity);

        foreach (var oldPart in order.Lines.Where(line => line.IsWarehousePart && line.WarehouseItemId.HasValue))
        {
            if (stock.TryGetValue(oldPart.WarehouseItemId!.Value, out var item)) item.Quantity += oldPart.Quantity;
        }
        var required = parts.GroupBy(item => item.WarehouseItemId!.Value).ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        foreach (var (itemId, quantity) in required)
        {
            if (!stock.TryGetValue(itemId, out var item)) ModelState.AddModelError(string.Empty, "A selected part no longer exists in Warehouse.");
            else if (item.Quantity < quantity) ModelState.AddModelError(string.Empty, $"Not enough stock for {item.PartName}. Available: {item.Quantity}; requested: {quantity}.");
        }
        if (!ModelState.IsValid)
        {
            await LoadAsync(id);
            return Page();
        }

        order.CustomerId = customer?.Id;
        order.CustomerDevice = device;
        order.CustomerDeviceId = device?.Id;
        order.Device = device?.DeviceType ?? Input.NewDeviceType?.Trim();
        order.DeviceModelAndSerialNumber = device?.ModelAndSerialNumber ?? Input.NewDeviceModelAndSerialNumber?.Trim();
        order.ProblemOrRepair = Input.ProblemOrRepair?.Trim();
        order.DeviceConditionAndNotes = Input.DeviceConditionAndNotes?.Trim();
        order.Accessories = BuildAccessories();
        order.DevicePassword = Input.DevicePassword;
        order.AdbDiagnosticReport = Input.AdbDiagnosticReport;

        foreach (var (itemId, quantity) in required) stock[itemId].Quantity -= quantity;

        foreach (var (itemId, quantityBefore) in quantitiesBefore)
        {
            var item = stock[itemId];
            if (item.Quantity == quantityBefore) continue;

            _dbContext.WarehouseAuditEntries.Add(new WarehouseAuditEntry
            {
                CompanyId = item.CompanyId,
                WarehouseItemId = item.Id,
                ItemName = item.PartName,
                ProductNumber = item.ProductNumber,
                Barcode = item.Barcode,
                Action = "Order parts updated",
                QuantityBefore = quantityBefore,
                QuantityAfter = item.Quantity,
                QuantityChange = item.Quantity - quantityBefore,
                UnitPrice = item.UnitPrice,
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                UserEmail = User.Identity?.Name ?? "Unknown user"
            });
        }

        _dbContext.ServiceOrderLines.RemoveRange(order.Lines);
        order.Lines = [
            .. parts.Select(item => new ServiceOrderLine { CompanyId = order.CompanyId, WarehouseItemId = item.WarehouseItemId!.Value, Description = stock[item.WarehouseItemId!.Value].PartName, UnitPrice = item.UnitPrice, Quantity = item.Quantity, IsWarehousePart = true }),
            .. services.Select(item => new ServiceOrderLine { CompanyId = order.CompanyId, Description = item.Description!.Trim(), UnitPrice = item.UnitPrice, Quantity = 1, IsWarehousePart = false })
        ];
        _dbContext.ServiceOrderLines.AddRange(order.Lines);
        var adjustmentsTotal = order.Adjustments.Sum(item => item.Amount);
        order.TotalPrice = parts.Sum(item => item.UnitPrice * item.Quantity) + services.Sum(item => item.UnitPrice) + adjustmentsTotal;
        var deletedPhotoPaths = new List<string>();
        if (device != null)
        {
            var photosToDelete = await _dbContext.CustomerDevicePhotos
                .Where(photo => photo.CustomerDeviceId == device.Id && Input.DeletedDevicePhotoIds.Contains(photo.Id))
                .ToListAsync();
            foreach (var photo in photosToDelete)
            {
                _dbContext.CustomerDevicePhotos.Remove(photo);
                deletedPhotoPaths.Add(Path.Combine(_environment.WebRootPath, "uploads", "customer-devices", photo.FileName));
            }
            if (Input.DevicePhotos.Count > 0)
                await SaveDevicePhotosAsync(device, Input.DevicePhotos);
        }
        await _dbContext.SaveChangesAsync();
        foreach (var path in deletedPhotoPaths)
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }

        TempData["StatusMessage"] = "Order and price have been updated.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostScanAdbAsync(int id)
    {
        var order = await _dbContext.ServiceOrders
            .Include(item => item.CustomerDevice)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (order == null) return NotFound();

        var scan = await _adbDiagnosticService.ScanAsync();
        if (!scan.Success)
        {
            TempData["AdbError"] = scan.Error;
            return RedirectToPage(new { id });
        }

        var modelAndSerial = $"{scan.Manufacturer} {scan.Model} — SN: {scan.SerialNumber}";
        order.Device = "Phone";
        order.DeviceModelAndSerialNumber = modelAndSerial;
        order.AdbDiagnosticReport = scan.Report;

        if (order.CustomerDevice != null)
        {
            order.CustomerDevice.DeviceType = "Phone";
            order.CustomerDevice.ModelAndSerialNumber = modelAndSerial;
        }

        await _dbContext.SaveChangesAsync();
        TempData["AdbStatus"] = $"Phone scanned: {scan.Manufacturer} {scan.Model}. Device details and diagnostic report were saved.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(int id)
    {
        Order = await _dbContext.ServiceOrders
            .Include(item => item.Customer)
            .Include(item => item.CustomerDevice!)
                .ThenInclude(device => device.Photos)
            .Include(item => item.Lines)
            .Include(item => item.Adjustments)
            .FirstOrDefaultAsync(item => item.Id == id);
        WarehouseItems = await _dbContext.WarehouseItems.OrderBy(item => item.PartName).ToListAsync();
        Customers = await _dbContext.Users.OrderBy(user => user.CustomerName ?? user.Email).ToListAsync();
        CustomerDevices = await _dbContext.CustomerDevices.Include(item => item.Photos).OrderBy(item => item.DeviceType).ToListAsync();
        AdbReport = ParseDiagnosticReport(Order?.AdbDiagnosticReport);
        return Order != null;
    }

    private async Task SaveDevicePhotosAsync(CustomerDevice device, IEnumerable<IFormFile> files)
    {
        var directory = Path.Combine(_environment.WebRootPath, "uploads", "customer-devices");
        Directory.CreateDirectory(directory);
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            await using (var stream = System.IO.File.Create(Path.Combine(directory, fileName)))
            {
                await file.CopyToAsync(stream);
            }
            _dbContext.CustomerDevicePhotos.Add(new CustomerDevicePhoto
            {
                CompanyId = device.CompanyId,
                CustomerDeviceId = device.Id,
                FileName = fileName,
                OriginalFileName = Path.GetFileName(file.FileName),
                ContentType = file.ContentType
            });
        }
    }

    private string? BuildAccessories()
    {
        var accessories = (Input.Accessories ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (accessories.Count == 0)
        {
            accessories = Input.SelectedAccessories
                .Where(accessory => !string.Equals(accessory, "Other", StringComparison.OrdinalIgnoreCase))
                .Select(accessory => accessory.Trim())
                .Where(accessory => accessory.Length > 0)
                .ToList();
            if (Input.SelectedAccessories.Any(accessory => string.Equals(accessory, "Other", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(Input.OtherAccessory))
                accessories.Add(Input.OtherAccessory.Trim());
        }
        return accessories.Count == 0 ? null : string.Join(", ", accessories.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsAllowedPhoto(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return file.Length > 0 &&
            new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(extension) &&
            file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static OrdersModel.AdbDiagnosticDetails? ParseDiagnosticReport(string? report)
    {
        if (string.IsNullOrWhiteSpace(report)) return null;

        var lines = report.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        static string Value(string[] lines, string label) => lines.FirstOrDefault(line => line.StartsWith(label, StringComparison.OrdinalIgnoreCase))?.Split(':', 2).ElementAtOrDefault(1)?.Trim() ?? "Unknown";
        var storageIndex = Array.FindIndex(lines, line => line.StartsWith("Internal storage", StringComparison.OrdinalIgnoreCase));
        var storage = storageIndex >= 0 && storageIndex + 1 < lines.Length
            ? string.Join(Environment.NewLine, lines.Skip(storageIndex + 1))
            : "Unavailable";

        return new OrdersModel.AdbDiagnosticDetails(
            Value(lines, "Manufacturer"),
            Value(lines, "Model"),
            Value(lines, "Android version"),
            Value(lines, "Serial number"),
            Value(lines, "Battery level").TrimEnd('%'),
            Value(lines, "Battery health"),
            Value(lines, "Battery temperature"),
            storage);
    }

    private void PopulateInputs()
    {
        Input.CustomerId = Order!.CustomerId;
        Input.CustomerDeviceId = Order.CustomerDeviceId;
        Input.NewDeviceType = Order.CustomerDeviceId.HasValue ? null : Order.Device;
        Input.NewDeviceModelAndSerialNumber = Order.CustomerDeviceId.HasValue ? null : Order.DeviceModelAndSerialNumber;
        Input.ProblemOrRepair = Order.ProblemOrRepair;
        Input.DeviceConditionAndNotes = Order.DeviceConditionAndNotes;
        Input.Accessories = Order.Accessories;
        var accessoryValues = (Order.Accessories ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var knownAccessories = new[] { "SIM card", "Case", "Charger", "Cable", "Bag" };
        Input.SelectedAccessories = accessoryValues.Where(value => knownAccessories.Contains(value, StringComparer.OrdinalIgnoreCase)).ToList();
        var customAccessory = accessoryValues.Where(value => !knownAccessories.Contains(value, StringComparer.OrdinalIgnoreCase)).ToList();
        if (customAccessory.Count > 0)
        {
            Input.SelectedAccessories.Add("Other");
            Input.OtherAccessory = string.Join(", ", customAccessory);
        }
        Input.DevicePassword = Order.DevicePassword;
        Input.AdbDiagnosticReport = Order.AdbDiagnosticReport;
        Parts = Order.Lines.Where(line => line.IsWarehousePart).Select(line => new PartInput { OrderLineId = line.Id, WarehouseItemId = line.WarehouseItemId ?? 0, UnitPrice = line.UnitPrice, Quantity = line.Quantity }).ToList();
        Services = Order.Lines.Where(line => !line.IsWarehousePart).Select(line => new ServiceInput { OrderLineId = line.Id, Description = line.Description, UnitPrice = line.UnitPrice }).ToList();
    }

    public class OrderEditInput
    {
        public string? CustomerId { get; set; }
        public int? CustomerDeviceId { get; set; }
        public string? NewDeviceType { get; set; }
        public string? NewDeviceModelAndSerialNumber { get; set; }
        public string? ProblemOrRepair { get; set; }
        public string? DeviceConditionAndNotes { get; set; }
        public string? Accessories { get; set; }
        public List<string> SelectedAccessories { get; set; } = [];
        public string? OtherAccessory { get; set; }
        public string? DevicePassword { get; set; }
        public string? AdbDiagnosticReport { get; set; }
        public List<IFormFile> DevicePhotos { get; set; } = [];
        public List<int> DeletedDevicePhotoIds { get; set; } = [];
    }

    public class PartInput { public int OrderLineId { get; set; } public int? WarehouseItemId { get; set; } public decimal UnitPrice { get; set; } public int Quantity { get; set; } = 1; }
    public class ServiceInput { public int OrderLineId { get; set; } public string? Description { get; set; } public decimal UnitPrice { get; set; } }
}
