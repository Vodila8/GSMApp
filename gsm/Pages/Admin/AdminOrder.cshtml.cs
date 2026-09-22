using System.Security.Claims;
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
    public AdminOrderModel(ApplicationDbContext dbContext, AdbDiagnosticService adbDiagnosticService)
    {
        _dbContext = dbContext;
        _adbDiagnosticService = adbDiagnosticService;
    }

    public ServiceOrder? Order { get; private set; }
    public List<WarehouseItem> WarehouseItems { get; private set; } = [];
    public OrdersModel.AdbDiagnosticDetails? AdbReport { get; private set; }

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
            .FirstOrDefaultAsync(item => item.Id == id);
        if (order == null) return NotFound();

        var parts = Parts.Where(item => item.WarehouseItemId.HasValue && item.WarehouseItemId.Value > 0).ToList();
        var services = Services.Where(item => !string.IsNullOrWhiteSpace(item.Description)).ToList();
        if (parts.Count == 0 && services.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one part or service.");
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
        await _dbContext.SaveChangesAsync();

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
        Order = await _dbContext.ServiceOrders.Include(item => item.Customer).Include(item => item.Lines).Include(item => item.Adjustments).FirstOrDefaultAsync(item => item.Id == id);
        WarehouseItems = await _dbContext.WarehouseItems.OrderBy(item => item.PartName).ToListAsync();
        AdbReport = ParseDiagnosticReport(Order?.AdbDiagnosticReport);
        return Order != null;
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
        Parts = Order!.Lines.Where(line => line.IsWarehousePart).Select(line => new PartInput { OrderLineId = line.Id, WarehouseItemId = line.WarehouseItemId ?? 0, UnitPrice = line.UnitPrice, Quantity = line.Quantity }).ToList();
        Services = Order.Lines.Where(line => !line.IsWarehousePart).Select(line => new ServiceInput { OrderLineId = line.Id, Description = line.Description, UnitPrice = line.UnitPrice }).ToList();
    }

    public class PartInput { public int OrderLineId { get; set; } public int? WarehouseItemId { get; set; } public decimal UnitPrice { get; set; } public int Quantity { get; set; } = 1; }
    public class ServiceInput { public int OrderLineId { get; set; } public string? Description { get; set; } public decimal UnitPrice { get; set; } }
}
