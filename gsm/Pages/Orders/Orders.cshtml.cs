using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using gsm.Data;
using gsm.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages;

[Authorize(Roles = "Boss,Administrator")]
public class OrdersModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly AdbDiagnosticService _adbDiagnosticService;
    private readonly IWebHostEnvironment _environment;

    public OrdersModel(ApplicationDbContext dbContext, TenantContext tenantContext, AdbDiagnosticService adbDiagnosticService, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _adbDiagnosticService = adbDiagnosticService;
        _environment = environment;
    }

    [BindProperty]
    public OrderInput Input { get; set; } = new();

    public List<ApplicationUser> Customers { get; private set; } = [];
    public List<WarehouseItem> WarehouseItems { get; private set; } = [];
    public List<CustomerDevice> CustomerDevices { get; private set; } = [];
    public string? AdbScanError { get; private set; }
    public AdbDiagnosticDetails? AdbReport { get; private set; }
    public bool HasAdbScan => !string.IsNullOrWhiteSpace(Input.AdbDiagnosticReport);

    public async Task OnGetAsync(string? deviceType = null, string? deviceDetails = null, string? adbReport = null)
    {
        await LoadOptionsAsync();

        if (!string.IsNullOrWhiteSpace(deviceType) || !string.IsNullOrWhiteSpace(deviceDetails) || !string.IsNullOrWhiteSpace(adbReport))
        {
            Input.NewDeviceType = deviceType ?? "Phone";
            Input.NewDeviceModelAndSerialNumber = deviceDetails;
            Input.AdbDiagnosticReport = adbReport;
            Input.Services[0].Description = "Device diagnostics";
            Input.Services[0].UnitPrice = 0;
        }
    }

    public async Task<IActionResult> OnPostScanAsync()
    {
        await LoadOptionsAsync();

        var scan = await _adbDiagnosticService.ScanAsync();
        if (!scan.Success)
        {
            AdbScanError = scan.Error;
            return Page();
        }

        Input.AdbDiagnosticReport = scan.Report;
        AdbReport = new AdbDiagnosticDetails(scan.Manufacturer, scan.Model, scan.AndroidVersion, scan.SerialNumber, scan.BatteryLevel, scan.BatteryHealth, scan.BatteryTemperature, scan.Storage);

        // A connected Android device is a phone. Pre-fill a new-device record,
        // while leaving a selected existing device untouched.
        if (!Input.CustomerDeviceId.HasValue)
        {
            Input.NewDeviceType = "Phone";
            Input.NewDeviceModelAndSerialNumber = $"{scan.Manufacturer} {scan.Model} — SN: {scan.SerialNumber}";
        }

        TempData["StatusMessage"] = "Phone scanned successfully with ADB. The diagnostic report will be attached to this repair when it is created.";
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        await LoadOptionsAsync();

        if (Input.DevicePhotos.Count > 10)
        {
            ModelState.AddModelError("Input.DevicePhotos", "You can upload up to 10 photos at a time.");
        }
        foreach (var photo in Input.DevicePhotos)
        {
            if (photo.Length > 10 * 1024 * 1024 || !IsAllowedPhoto(photo))
            {
                ModelState.AddModelError("Input.DevicePhotos", "Photos must be JPG, PNG, GIF or WEBP files up to 10 MB each.");
            }
        }

        var customer = await _dbContext.Users.FirstOrDefaultAsync(user => user.Id == Input.CustomerId && user.CompanyId == _tenantContext.CompanyId);
        if (customer == null || await IsEmployeeAsync(Input.CustomerId))
        {
            ModelState.AddModelError("Input.CustomerId", "Select a registered customer.");
        }

        CustomerDevice? device = null;
        if (Input.CustomerDeviceId.HasValue)
        {
            device = await _dbContext.CustomerDevices.FirstOrDefaultAsync(item =>
                item.Id == Input.CustomerDeviceId && item.CustomerId == Input.CustomerId);
            if (device == null)
            {
                ModelState.AddModelError("Input.CustomerDeviceId", "Select one of this customer's devices or add a new device.");
            }
        }
        else if (string.IsNullOrWhiteSpace(Input.NewDeviceType))
        {
            ModelState.AddModelError("Input.NewDeviceType", "Select a device or enter a new device.");
        }

        var parts = Input.Parts.Where(part => part.WarehouseItemId.HasValue && part.WarehouseItemId.Value > 0).ToList();
        var services = Input.Services.Where(service => !string.IsNullOrWhiteSpace(service.Description)).ToList();

        foreach (var part in parts)
        {
            if (part.Quantity <= 0 || part.UnitPrice < 0)
            {
                ModelState.AddModelError(string.Empty, "Part quantity must be greater than zero and the price cannot be negative.");
            }
        }

        foreach (var service in services)
        {
            if (service.UnitPrice < 0)
            {
                ModelState.AddModelError(string.Empty, "Service price cannot be negative.");
            }
        }

        if (!ModelState.IsValid || customer == null)
        {
            return Page();
        }

        var requiredQuantities = parts
            .GroupBy(part => part.WarehouseItemId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(part => part.Quantity));

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        if (device == null)
        {
            device = new CustomerDevice
            {
                CompanyId = _tenantContext.CompanyId,
                CustomerId = customer.Id,
                DeviceType = Input.NewDeviceType!.Trim(),
                ModelAndSerialNumber = Input.NewDeviceModelAndSerialNumber?.Trim()
            };
            _dbContext.CustomerDevices.Add(device);
        }

        var stock = await _dbContext.WarehouseItems
            .Where(item => requiredQuantities.Keys.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id);

        foreach (var (itemId, requiredQuantity) in requiredQuantities)
        {
            if (!stock.TryGetValue(itemId, out var item))
            {
                ModelState.AddModelError(string.Empty, "One of the selected parts no longer exists in the warehouse.");
            }
            else if (item.Quantity < requiredQuantity)
            {
                ModelState.AddModelError(string.Empty, $"Not enough stock for {item.PartName}. Available: {item.Quantity}; requested: {requiredQuantity}.");
            }
        }

        if (!ModelState.IsValid)
        {
            await transaction.RollbackAsync();
            return Page();
        }

        foreach (var (itemId, requiredQuantity) in requiredQuantities)
        {
            var item = stock[itemId];
            var quantityBefore = item.Quantity;
            item.Quantity -= requiredQuantity;
            _dbContext.WarehouseAuditEntries.Add(new WarehouseAuditEntry
            {
                CompanyId = item.CompanyId,
                WarehouseItemId = item.Id,
                ItemName = item.PartName,
                ProductNumber = item.ProductNumber,
                Barcode = item.Barcode,
                Action = "Used in repair order",
                QuantityBefore = quantityBefore,
                QuantityAfter = item.Quantity,
                QuantityChange = -requiredQuantity,
                UnitPrice = item.UnitPrice,
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                UserEmail = User.Identity?.Name ?? "Unknown user"
            });
        }

        var order = new ServiceOrder
        {
            CompanyId = _tenantContext.CompanyId,
            CustomerId = customer.Id,
            CustomerDevice = device,
            Device = device.DeviceType,
            DeviceModelAndSerialNumber = device.ModelAndSerialNumber,
            ProblemOrRepair = Input.ProblemOrRepair?.Trim(),
            DeviceConditionAndNotes = Input.DeviceConditionAndNotes?.Trim(),
            Accessories = BuildAccessories(),
            DevicePassword = Input.DevicePassword,
            AdbDiagnosticReport = Input.AdbDiagnosticReport,
            TotalPrice = parts.Sum(part => part.UnitPrice * part.Quantity) + services.Sum(service => service.UnitPrice),
            Lines = [
                .. parts.Select(part => new ServiceOrderLine
                {
                    CompanyId = _tenantContext.CompanyId!,
                    WarehouseItemId = part.WarehouseItemId!.Value,
                    Description = stock[part.WarehouseItemId!.Value].PartName,
                    UnitPrice = part.UnitPrice,
                    Quantity = part.Quantity,
                    IsWarehousePart = true
                }),
                .. services.Select(service => new ServiceOrderLine
                {
                    CompanyId = _tenantContext.CompanyId!,
                    Description = service.Description.Trim(),
                    UnitPrice = service.UnitPrice,
                    Quantity = 1,
                    IsWarehousePart = false
                })
            ],
            Stages = [
                new OrderStage { CompanyId = _tenantContext.CompanyId!, Name = "Start", SortOrder = 0, IsFixed = true },
                .. services.Select((service, index) => new OrderStage
                {
                    CompanyId = _tenantContext.CompanyId!,
                    Name = service.Description.Trim(),
                    SortOrder = index + 1
                }),
                new OrderStage { CompanyId = _tenantContext.CompanyId!, Name = "End", SortOrder = services.Count + 1, IsFixed = true }
            ]
        };

        _dbContext.ServiceOrders.Add(order);
        await _dbContext.SaveChangesAsync();

        var deletedPhotoPaths = new List<string>();
        if (Input.CustomerDeviceId.HasValue)
        {
            var photosToDelete = await _dbContext.CustomerDevicePhotos
                .Where(photo => photo.CustomerDeviceId == device.Id && Input.DeletedDevicePhotoIds.Contains(photo.Id))
                .ToListAsync();
            foreach (var photo in photosToDelete)
            {
                _dbContext.CustomerDevicePhotos.Remove(photo);
                deletedPhotoPaths.Add(Path.Combine(_environment.WebRootPath, "uploads", "customer-devices", photo.FileName));
            }
        }
        if (Input.DevicePhotos.Count > 0)
        {
            await SaveDevicePhotosAsync(device, Input.DevicePhotos);
        }

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        foreach (var path in deletedPhotoPaths)
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }

        return Redirect($"/OrderTracking/{order.Id}");
    }

    private async Task LoadOptionsAsync()
    {
        // Older customer accounts stored their first device directly on the user record.
        // Copy it once into the reusable devices list so it is available in the selector.
        var usersWithLegacyDevices = await _dbContext.Users
            .Where(user => !string.IsNullOrWhiteSpace(user.Device))
            .Select(user => new { user.Id, user.CompanyId, user.Device, user.DeviceModelAndSerialNumber })
            .ToListAsync();
        var customersWithSavedDevices = await _dbContext.CustomerDevices
            .Select(device => device.CustomerId)
            .Distinct()
            .ToListAsync();

        foreach (var user in usersWithLegacyDevices.Where(user => !customersWithSavedDevices.Contains(user.Id)))
        {
            _dbContext.CustomerDevices.Add(new CustomerDevice
            {
                CustomerId = user.Id,
                CompanyId = user.CompanyId ?? string.Empty,
                DeviceType = user.Device!,
                ModelAndSerialNumber = user.DeviceModelAndSerialNumber
            });
        }

        await _dbContext.SaveChangesAsync();

        var employeeIds = _dbContext.UserRoles
            .Join(_dbContext.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.NormalizedName })
            .Where(item => item.NormalizedName == "ADMINISTRATOR" || item.NormalizedName == "BOSS" || item.NormalizedName == "TECHNICIAN")
            .Select(item => item.UserId);

        Customers = await _dbContext.Users
            .Where(user => user.CompanyId == _tenantContext.CompanyId && user.Status == UserStatus.Active && !employeeIds.Contains(user.Id))
            .OrderBy(user => user.CustomerName ?? user.Email)
            .ToListAsync();
        WarehouseItems = await _dbContext.WarehouseItems.OrderBy(item => item.PartName).ToListAsync();
        CustomerDevices = await _dbContext.CustomerDevices
            .Include(item => item.Photos)
            .OrderBy(item => item.DeviceType)
            .ToListAsync();
    }

    private async Task SaveDevicePhotosAsync(CustomerDevice device, IEnumerable<IFormFile> files)
    {
        var directory = Path.Combine(_environment.WebRootPath, "uploads", "customer-devices");
        Directory.CreateDirectory(directory);

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var path = Path.Combine(directory, fileName);
            await using (var stream = System.IO.File.Create(path))
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

    private static bool IsAllowedPhoto(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return file.Length > 0 &&
            new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(extension) &&
            file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
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

            if (Input.SelectedAccessories.Any(accessory => string.Equals(accessory, "Other", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(Input.OtherAccessory))
            {
                accessories.Add(Input.OtherAccessory.Trim());
            }
        }

        return accessories.Count == 0 ? null : string.Join(", ", accessories.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private Task<bool> IsEmployeeAsync(string userId) =>
        _dbContext.UserRoles
            .Join(_dbContext.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.NormalizedName })
            .AnyAsync(item => item.UserId == userId &&
                (item.NormalizedName == "ADMINISTRATOR" || item.NormalizedName == "BOSS" || item.NormalizedName == "TECHNICIAN"));

    public record AdbDiagnosticDetails(
        string Manufacturer,
        string Model,
        string AndroidVersion,
        string SerialNumber,
        string BatteryLevel,
        string BatteryHealth,
        string BatteryTemperature,
        string Storage);

    public class OrderInput
    {
        public string CustomerId { get; set; } = string.Empty;
        public int? CustomerDeviceId { get; set; }
        public string? NewDeviceType { get; set; }
        public string? NewDeviceModelAndSerialNumber { get; set; }
        public List<IFormFile> DevicePhotos { get; set; } = [];
        public List<int> DeletedDevicePhotoIds { get; set; } = [];
        public string? ProblemOrRepair { get; set; }
        public string? DeviceConditionAndNotes { get; set; }
        public string? Accessories { get; set; }
        public List<string> SelectedAccessories { get; set; } = [];
        public string? OtherAccessory { get; set; }
        public string? DevicePassword { get; set; }
        public string? AdbDiagnosticReport { get; set; }
        public List<PartInput> Parts { get; set; } = [new()];
        public List<ServiceInput> Services { get; set; } = [new()];
    }

    public class PartInput
    {
        public int? WarehouseItemId { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class ServiceInput
    {
        public string Description { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
    }
}
