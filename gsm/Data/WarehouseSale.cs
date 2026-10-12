using System.ComponentModel.DataAnnotations;

namespace gsm.Data;

public class WarehouseSale
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string CompanyId { get; set; } = string.Empty;

    public int PartnerId { get; set; }
    public WarehousePartner Partner { get; set; } = null!;

    public int WarehouseItemId { get; set; }
    public WarehouseItem WarehouseItem { get; set; } = null!;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public decimal DiscountPercent { get; set; }

    public decimal TotalAmount { get; set; }

    public DateOnly? SaleDate { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    [Required, MaxLength(256)]
    public string UserEmail { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
