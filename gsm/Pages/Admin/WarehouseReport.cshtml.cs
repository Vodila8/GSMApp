using gsm.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages;

[Authorize(Roles = "Boss,Administrator,Technician")]
public class WarehouseReportModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public WarehouseReportModel(ApplicationDbContext dbContext) => _dbContext = dbContext;

    [BindProperty(SupportsGet = true)]
    public string Type { get; set; } = "sales";

    [BindProperty(SupportsGet = true)]
    public DateOnly? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? ToDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ProductNumber { get; set; }

    public List<WarehouseSale> Sales { get; private set; } = [];
    public List<WarehouseItem> Deliveries { get; private set; } = [];
    public bool IsDeliveryReport => string.Equals(Type, "deliveries", StringComparison.OrdinalIgnoreCase);

    public async Task OnGetAsync()
    {
        Type = IsDeliveryReport ? "deliveries" : "sales";
        var productFilter = ProductNumber?.Trim();
        if (IsDeliveryReport)
        {
            Deliveries = await _dbContext.WarehouseItems
                .Include(item => item.Partner)
                .Where(item => item.DeliveryDate.HasValue)
                .OrderByDescending(item => item.DeliveryDate)
                .ThenBy(item => item.PartName)
                .ToListAsync();
            Deliveries = Deliveries.Where(item => MatchesDate(item.DeliveryDate!.Value) && MatchesProduct(item.ProductNumber, productFilter)).ToList();
        }
        else
        {
            Sales = await _dbContext.WarehouseSales
                .Include(sale => sale.Partner)
                .Include(sale => sale.WarehouseItem)
                .OrderByDescending(sale => sale.CreatedAt)
                .ToListAsync();
            Sales = Sales.Where(sale => MatchesDate(sale.SaleDate ?? DateOnly.FromDateTime(sale.CreatedAt.ToLocalTime())) &&
                                       MatchesProduct(sale.WarehouseItem.ProductNumber ?? sale.WarehouseItem.Id.ToString(), productFilter)).ToList();
        }
    }

    private bool MatchesDate(DateOnly date) =>
        (!FromDate.HasValue || date >= FromDate.Value) && (!ToDate.HasValue || date <= ToDate.Value);

    private static bool MatchesProduct(string? productNumber, string? filter) =>
        string.IsNullOrWhiteSpace(filter) || (!string.IsNullOrWhiteSpace(productNumber) && productNumber.Contains(filter, StringComparison.OrdinalIgnoreCase));
}
