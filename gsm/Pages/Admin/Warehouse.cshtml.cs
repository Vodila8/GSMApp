using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using gsm.Data;
using gsm.Services;
using Microsoft.AspNetCore.Hosting;
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
    private readonly IWebHostEnvironment _environment;

    public WarehouseModel(ApplicationDbContext dbContext, TenantContext tenantContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _environment = environment;
    }

    [BindProperty]
    public List<WarehouseItemInput> Items { get; set; } = [];

    [BindProperty]
    public NewWarehouseItemInput NewItem { get; set; } = new();

    [BindProperty]
    public WarehouseSaleInput Sale { get; set; } = new();

    public bool ShowSaleModal { get; private set; }
    public List<string> ProductNumbers { get; private set; } = [];
    public List<WarehousePartner> Partners { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

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

        if (NewItem.Photos.Count > 10)
        {
            ModelState.AddModelError("NewItem.Photos", "You can upload up to 10 photos at a time.");
        }
        foreach (var photo in NewItem.Photos)
        {
            if (photo.Length > 10 * 1024 * 1024 || !IsAllowedPhoto(photo))
            {
                ModelState.AddModelError("NewItem.Photos", "Photos must be JPG, PNG, GIF or WEBP files up to 10 MB each.");
            }
        }
        for (var index = 0; index < Items.Count; index++)
        {
            if (Items[index].NewPhotos.Count > 10)
            {
                ModelState.AddModelError($"Items[{index}].NewPhotos", "You can upload up to 10 photos at a time.");
            }
            foreach (var photo in Items[index].NewPhotos)
            {
                if (photo.Length > 10 * 1024 * 1024 || !IsAllowedPhoto(photo))
                {
                    ModelState.AddModelError($"Items[{index}].NewPhotos", "Photos must be JPG, PNG, GIF or WEBP files up to 10 MB each.");
                }
            }
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

        WarehouseItem? createdItem = null;
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

            createdItem = new WarehouseItem
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
            _dbContext.WarehouseItems.Add(createdItem);
            auditEntries.Add(new WarehouseAuditEntry
            {
                CompanyId = _tenantContext.CompanyId,
                ItemName = createdItem.PartName,
                ProductNumber = createdItem.ProductNumber,
                Barcode = createdItem.Barcode,
                Action = "Item added",
                QuantityBefore = 0,
                QuantityAfter = createdItem.Quantity,
                QuantityChange = createdItem.Quantity,
                UnitPrice = createdItem.UnitPrice,
                UserId = userId,
                UserEmail = userEmail
            });
        }

        var deletedPhotoPaths = new List<string>();
        foreach (var input in Items)
        {
            if (!existingItems.TryGetValue(input.Id, out var item))
            {
                continue;
            }

            if (input.DeletedPhotoIds.Count > 0)
            {
                var photosToDelete = await _dbContext.WarehouseItemPhotos
                    .Where(photo => photo.WarehouseItemId == item.Id &&
                                    photo.CompanyId == _tenantContext.CompanyId &&
                                    input.DeletedPhotoIds.Contains(photo.Id))
                    .ToListAsync();
                foreach (var photo in photosToDelete)
                {
                    _dbContext.WarehouseItemPhotos.Remove(photo);
                    deletedPhotoPaths.Add(Path.Combine(_environment.WebRootPath, "uploads", "warehouse", photo.FileName));
                }
            }

            if (input.NewPhotos.Count > 0)
            {
                await SavePhotosAsync(item, input.NewPhotos);
            }
        }

        _dbContext.WarehouseAuditEntries.AddRange(auditEntries);
        await _dbContext.SaveChangesAsync();
        foreach (var path in deletedPhotoPaths)
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }

        if (createdItem != null && NewItem.Photos.Count > 0)
        {
            await SavePhotosAsync(createdItem, NewItem.Photos);
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeletePhotoAsync(int id)
    {
        if (string.IsNullOrWhiteSpace(_tenantContext.CompanyId)) return Forbid();

        var photo = await _dbContext.WarehouseItemPhotos.FirstOrDefaultAsync(itemPhoto =>
            itemPhoto.Id == id && itemPhoto.CompanyId == _tenantContext.CompanyId);
        if (photo == null) return NotFound();

        _dbContext.WarehouseItemPhotos.Remove(photo);
        await _dbContext.SaveChangesAsync();
        var path = Path.Combine(_environment.WebRootPath, "uploads", "warehouse", photo.FileName);
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSellAsync()
    {
        ShowSaleModal = true;
        if (string.IsNullOrWhiteSpace(_tenantContext.CompanyId)) return Forbid();

        var item = await _dbContext.WarehouseItems.FirstOrDefaultAsync(warehouseItem =>
            warehouseItem.Id == Sale.WarehouseItemId && warehouseItem.CompanyId == _tenantContext.CompanyId);
        if (item == null)
        {
            ModelState.AddModelError("Sale.WarehouseItemId", "Select a valid warehouse item.");
        }

        if (Sale.Quantity <= 0)
        {
            ModelState.AddModelError("Sale.Quantity", "Quantity must be greater than zero.");
        }
        else if (item != null && Sale.Quantity > item.Quantity)
        {
            ModelState.AddModelError("Sale.Quantity", $"Only {item.Quantity} items are available.");
        }

        if (Sale.DiscountPercent < 0 || Sale.DiscountPercent > 100)
        {
            ModelState.AddModelError("Sale.DiscountPercent", "Discount must be between 0 and 100 percent.");
        }

        if (Sale.PartnerId.HasValue && !string.IsNullOrWhiteSpace(Sale.NewPartnerName))
        {
            ModelState.AddModelError("Sale.PartnerId", "Select an existing partner or enter a new partner, not both.");
        }
        else if (!Sale.PartnerId.HasValue && string.IsNullOrWhiteSpace(Sale.NewPartnerName))
        {
            ModelState.AddModelError("Sale.PartnerId", "Select an existing partner or enter a new partner.");
        }
        else if (Sale.PartnerId.HasValue && !await _dbContext.WarehousePartners.AnyAsync(partner =>
                     partner.Id == Sale.PartnerId.Value && partner.CompanyId == _tenantContext.CompanyId))
        {
            ModelState.AddModelError("Sale.PartnerId", "Select a valid partner.");
        }

        if (!ModelState.IsValid)
        {
            await LoadItemsAsync();
            return Page();
        }

        WarehousePartner? newPartner = null;
        if (!string.IsNullOrWhiteSpace(Sale.NewPartnerName))
        {
            newPartner = await _dbContext.WarehousePartners.FirstOrDefaultAsync(partner =>
                partner.CompanyId == _tenantContext.CompanyId && partner.Name == Sale.NewPartnerName.Trim());
            if (newPartner == null)
            {
                newPartner = new WarehousePartner
                {
                    CompanyId = _tenantContext.CompanyId,
                    Name = Sale.NewPartnerName.Trim()
                };
                _dbContext.WarehousePartners.Add(newPartner);
            }
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        var quantityBefore = item!.Quantity;
        item.Quantity -= Sale.Quantity;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.Identity?.Name ?? "Unknown user";
        var totalAmount = Math.Round(item.UnitPrice * Sale.Quantity * (1 - Sale.DiscountPercent / 100m), 2);
        var sale = new WarehouseSale
        {
            CompanyId = _tenantContext.CompanyId,
            PartnerId = Sale.PartnerId ?? 0,
            Partner = newPartner,
            WarehouseItemId = item.Id,
            WarehouseItem = item,
            Quantity = Sale.Quantity,
            UnitPrice = item.UnitPrice,
            DiscountPercent = Sale.DiscountPercent,
            TotalAmount = totalAmount,
            Note = Sale.Note?.Trim(),
            UserId = userId,
            UserEmail = userEmail
        };
        _dbContext.WarehouseSales.Add(sale);
        _dbContext.WarehouseAuditEntries.Add(new WarehouseAuditEntry
        {
            CompanyId = _tenantContext.CompanyId,
            WarehouseItemId = item.Id,
            ItemName = item.PartName,
            ProductNumber = item.ProductNumber,
            Barcode = item.Barcode,
            Action = "Sale",
            QuantityBefore = quantityBefore,
            QuantityAfter = item.Quantity,
            QuantityChange = -Sale.Quantity,
            UnitPrice = item.UnitPrice,
            UserId = userId,
            UserEmail = userEmail
        });
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["StatusMessage"] = $"Sold {Sale.Quantity} × {item.PartName}.";
        return RedirectToPage();
    }

    private async Task SavePhotosAsync(WarehouseItem item, IEnumerable<IFormFile> files)
    {
        var directory = Path.Combine(_environment.WebRootPath, "uploads", "warehouse");
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

            _dbContext.WarehouseItemPhotos.Add(new WarehouseItemPhoto
            {
                CompanyId = item.CompanyId,
                WarehouseItemId = item.Id,
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

    private async Task LoadItemsAsync()
    {
        var warehouseItems = await _dbContext.WarehouseItems
            .Include(item => item.Partner)
            .Include(item => item.Photos)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var search = Search.Trim();
            warehouseItems = warehouseItems.Where(item =>
                (!string.IsNullOrWhiteSpace(item.Barcode) && item.Barcode.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                item.PartName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (NormalizeProductNumber(item.ProductNumber)?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }
        Partners = await _dbContext.WarehousePartners.OrderBy(partner => partner.Name).ToListAsync();

        Items = warehouseItems.Select(item => new WarehouseItemInput
        {
            Id = item.Id,
            PartnerId = item.PartnerId,
            PartnerName = item.Partner?.Name,
            Photos = item.Photos.Select(photo => new WarehousePhotoInput
            {
                Id = photo.Id,
                FileName = photo.FileName,
                OriginalFileName = photo.OriginalFileName
            }).ToList(),
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
        public List<WarehousePhotoInput> Photos { get; set; } = [];
        public List<IFormFile> NewPhotos { get; set; } = [];
        public List<int> DeletedPhotoIds { get; set; } = [];

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

    public class WarehousePhotoInput
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? OriginalFileName { get; set; }
    }

    public class WarehouseSaleInput
    {
        public int WarehouseItemId { get; set; }
        public int? PartnerId { get; set; }
        public string? NewPartnerName { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal DiscountPercent { get; set; }
        public string? Note { get; set; }
    }

    public class NewWarehouseItemInput
    {
        public int? PartnerId { get; set; }
        public string? NewPartnerName { get; set; }
        public List<IFormFile> Photos { get; set; } = [];

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
