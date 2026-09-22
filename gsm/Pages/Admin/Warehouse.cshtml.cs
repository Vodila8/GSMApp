using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using gsm.Data;
using gsm.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages;

[Authorize(Roles = "Boss,Administrator,Technician")]
public class WarehouseModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TenantContext _tenantContext;

    public WarehouseModel(ApplicationDbContext dbContext, TenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    [BindProperty]
    public List<WarehouseItemInput> Items { get; set; } = [];

    [BindProperty]
    public NewWarehouseItemInput NewItem { get; set; } = new();

    public List<string> ProductNumbers { get; private set; } = [];
    public List<WarehousePartner> Partners { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await LoadItemsAsync();
        NewItem.ProductNumber ??= await GetNextProductNumberAsync();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(Items[index].PartName))
            {
                ModelState.AddModelError($"Items[{index}].PartName", "Part name is required.");
            }
            else if (Items[index].PartName.Length > 200)
            {
                ModelState.AddModelError($"Items[{index}].PartName", "Part name cannot exceed 200 characters.");
            }
        }

        if (!string.IsNullOrWhiteSpace(NewItem.PartName) && NewItem.PartName.Length > 200)
        {
            ModelState.AddModelError("NewItem.PartName", "Part name cannot exceed 200 characters.");
        }

        for (var index = 0; index < Items.Count; index++)
        {
            var productNumber = Items[index].ProductNumber;
            if (!string.IsNullOrWhiteSpace(productNumber) && !productNumber.All(char.IsDigit))
            {
                ModelState.AddModelError($"Items[{index}].ProductNumber", "Product number must contain digits only.");
            }
        }

        var newProductNumber = NewItem.ProductNumber;
        if (!string.IsNullOrWhiteSpace(newProductNumber) && !newProductNumber.All(char.IsDigit))
        {
            ModelState.AddModelError("NewItem.ProductNumber", "Product number must contain digits only.");
        }

        if (!string.IsNullOrWhiteSpace(NewItem.PartName))
        {
            if (NewItem.PartnerId.HasValue && !string.IsNullOrWhiteSpace(NewItem.NewPartnerName))
            {
                ModelState.AddModelError("NewItem.PartnerId", "Select an existing partner or enter a new partner, not both.");
            }
            else if (!NewItem.PartnerId.HasValue && string.IsNullOrWhiteSpace(NewItem.NewPartnerName))
            {
                ModelState.AddModelError("NewItem.PartnerId", "Select an existing partner or enter a new partner.");
            }
            else if (NewItem.PartnerId.HasValue && !await _dbContext.WarehousePartners.AnyAsync(partner => partner.Id == NewItem.PartnerId.Value && partner.CompanyId == _tenantContext.CompanyId))
            {
                ModelState.AddModelError("NewItem.PartnerId", "Select a valid partner.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var existingItems = await _dbContext.WarehouseItems
            .Where(item => Items.Select(input => input.Id).Contains(item.Id))
            .ToDictionaryAsync(item => item.Id);
        var auditEntries = new List<WarehouseAuditEntry>();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.Identity?.Name ?? "Unknown user";

        foreach (var input in Items)
        {
            if (!existingItems.TryGetValue(input.Id, out var item))
            {
                return NotFound();
            }

            var oldName = item.PartName;
            var oldProductNumber = item.ProductNumber;
            var oldBarcode = item.Barcode;
            var oldPrice = item.UnitPrice;
            var oldQuantity = item.Quantity;
            var newName = input.PartName.Trim();

            item.PartName = newName;
            item.ProductNumber = NormalizeProductNumber(input.ProductNumber);
            item.Barcode = input.Barcode?.Trim();
            item.UnitPrice = input.UnitPrice;
            item.DeliveryPrice = input.DeliveryPrice;
            item.Quantity = input.Quantity;

            if (oldName != item.PartName || oldProductNumber != item.ProductNumber || oldBarcode != item.Barcode || oldPrice != item.UnitPrice || oldQuantity != item.Quantity)
            {
                auditEntries.Add(new WarehouseAuditEntry
                {
                    CompanyId = item.CompanyId,
                    WarehouseItemId = item.Id,
                    ItemName = item.PartName,
                    ProductNumber = item.ProductNumber,
                    Barcode = item.Barcode,
                    Action = oldQuantity != item.Quantity ? "Quantity changed" : "Item details changed",
                    QuantityBefore = oldQuantity,
                    QuantityAfter = item.Quantity,
                    QuantityChange = item.Quantity - oldQuantity,
                    UnitPrice = item.UnitPrice,
                    UserId = userId,
                    UserEmail = userEmail
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(NewItem.PartName))
        {
            if (string.IsNullOrWhiteSpace(_tenantContext.CompanyId)) return Forbid();

            WarehousePartner? newPartner = null;
            if (!string.IsNullOrWhiteSpace(NewItem.NewPartnerName))
            {
                newPartner = await _dbContext.WarehousePartners.FirstOrDefaultAsync(partner =>
                    partner.CompanyId == _tenantContext.CompanyId && partner.Name == NewItem.NewPartnerName.Trim());
                if (newPartner == null)
                {
                    newPartner = new WarehousePartner
                    {
                        CompanyId = _tenantContext.CompanyId,
                        Name = NewItem.NewPartnerName.Trim()
                    };
                    _dbContext.WarehousePartners.Add(newPartner);
                }
            }

            var newItem = new WarehouseItem
            {
                CompanyId = _tenantContext.CompanyId,
                PartnerId = NewItem.PartnerId,
                Partner = newPartner,
                PartName = NewItem.PartName.Trim(),
                ProductNumber = string.IsNullOrWhiteSpace(NewItem.ProductNumber)
                    ? await GetNextProductNumberAsync()
                    : NormalizeProductNumber(NewItem.ProductNumber),
                Barcode = NewItem.Barcode?.Trim(),
                UnitPrice = NewItem.UnitPrice ?? 0,
                DeliveryPrice = NewItem.DeliveryPrice ?? 0,
                Quantity = NewItem.Quantity ?? 0
            };
            _dbContext.WarehouseItems.Add(newItem);
            auditEntries.Add(new WarehouseAuditEntry
            {
                CompanyId = _tenantContext.CompanyId,
                ItemName = newItem.PartName,
                ProductNumber = newItem.ProductNumber,
                Barcode = newItem.Barcode,
                Action = "Item added",
                QuantityBefore = 0,
                QuantityAfter = newItem.Quantity,
                QuantityChange = newItem.Quantity,
                UnitPrice = newItem.UnitPrice,
                UserId = userId,
                UserEmail = userEmail
            });
        }

        _dbContext.WarehouseAuditEntries.AddRange(auditEntries);
        await _dbContext.SaveChangesAsync();

        return RedirectToPage();
    }

    private async Task LoadItemsAsync()
    {
        var warehouseItems = await _dbContext.WarehouseItems
            .Include(item => item.Partner)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync();
        Partners = await _dbContext.WarehousePartners.OrderBy(partner => partner.Name).ToListAsync();

        Items = warehouseItems.Select(item => new WarehouseItemInput
        {
            Id = item.Id,
            PartnerId = item.PartnerId,
            PartnerName = item.Partner?.Name,
            PartName = item.PartName,
            ProductNumber = NormalizeProductNumber(item.ProductNumber) ?? item.Id.ToString(),
            Barcode = item.Barcode,
            UnitPrice = item.UnitPrice,
            DeliveryPrice = item.DeliveryPrice,
            Quantity = item.Quantity
        }).ToList();
        ProductNumbers = warehouseItems
            .Select(item => NormalizeProductNumber(item.ProductNumber) ?? item.Id.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(number => number)
            .ToList();
    }

    private async Task<string> GetNextProductNumberAsync()
    {
        var numbers = await _dbContext.WarehouseItems
            .Select(item => item.ProductNumber)
            .ToListAsync();
        var next = numbers
            .Select(ParseProductNumber)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return next.ToString();
    }

    private static string? NormalizeProductNumber(string? number)
    {
        if (string.IsNullOrWhiteSpace(number)) return null;
        var value = number.Trim();
        if (value.StartsWith("P-", StringComparison.OrdinalIgnoreCase) && int.TryParse(value[2..], out var legacyNumber))
        {
            return legacyNumber.ToString();
        }

        return value;
    }

    private static int ParseProductNumber(string? number)
    {
        var normalized = NormalizeProductNumber(number);
        return int.TryParse(normalized, out var value) ? value : 0;
    }

    public class WarehouseItemInput
    {
        public int Id { get; set; }
        public int? PartnerId { get; set; }
        public string? PartnerName { get; set; }

        [Display(Name = "Part Name")]
        public string PartName { get; set; } = string.Empty;

        [Display(Name = "Product Number")]
        public string? ProductNumber { get; set; }

        [Display(Name = "Barcode")]
        public string? Barcode { get; set; }

        [Range(0, 999999.99)]
        [Display(Name = "Unit Price (EUR)")]
        public decimal UnitPrice { get; set; }

        [Range(0, 999999.99)]
        [Display(Name = "Delivery Price (EUR)")]
        public decimal DeliveryPrice { get; set; }

        [Range(0, int.MaxValue)]
        [Display(Name = "Quantity")]
        public int Quantity { get; set; }
    }

    public class NewWarehouseItemInput
    {
        public int? PartnerId { get; set; }
        public string? NewPartnerName { get; set; }

        [Display(Name = "Part Name")]
        public string? PartName { get; set; }

        [Display(Name = "Product Number")]
        public string? ProductNumber { get; set; }

        [Display(Name = "Barcode")]
        public string? Barcode { get; set; }

        [Range(0, 999999.99)]
        [Display(Name = "Unit Price (EUR)")]
        public decimal? UnitPrice { get; set; }

        [Range(0, 999999.99)]
        [Display(Name = "Delivery Price (EUR)")]
        public decimal? DeliveryPrice { get; set; }

        [Range(0, int.MaxValue)]
        [Display(Name = "Quantity")]
        public int? Quantity { get; set; }
    }
}
