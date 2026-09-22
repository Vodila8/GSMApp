using gsm.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages;

[Authorize(Roles = "Boss,Administrator,Technician")]
public class WarehouseReportModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public WarehouseReportModel(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public List<AuditItem> Entries { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var auditEntries = await _dbContext.WarehouseAuditEntries
            .OrderByDescending(entry => entry.CreatedAt)
            .ToListAsync();
        var warehouseItems = await _dbContext.WarehouseItems.ToListAsync();

        Entries = auditEntries.Select(entry =>
        {
            var currentItem = warehouseItems.FirstOrDefault(item =>
                item.Id == entry.WarehouseItemId ||
                (entry.WarehouseItemId == null && item.PartName == entry.ItemName));

            return new AuditItem
            {
                ItemName = entry.ItemName,
                ProductNumber = entry.ProductNumber ?? currentItem?.ProductNumber ?? currentItem?.Id.ToString(),
                Barcode = entry.Barcode ?? currentItem?.Barcode,
                Action = entry.Action,
                QuantityBefore = entry.QuantityBefore,
                QuantityAfter = entry.QuantityAfter,
                QuantityChange = entry.QuantityChange,
                UnitPrice = entry.UnitPrice,
                UserEmail = entry.UserEmail,
                CreatedAt = entry.CreatedAt
            };
        }).ToList();
    }

    public class AuditItem
    {
        public string ItemName { get; set; } = string.Empty;
        public string? ProductNumber { get; set; }
        public string? Barcode { get; set; }
        public string Action { get; set; } = string.Empty;
        public int QuantityBefore { get; set; }
        public int QuantityAfter { get; set; }
        public int QuantityChange { get; set; }
        public decimal UnitPrice { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
