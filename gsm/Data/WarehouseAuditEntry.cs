using System.ComponentModel.DataAnnotations;

namespace gsm.Data;

public class WarehouseAuditEntry
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string CompanyId { get; set; } = string.Empty;

    public int? WarehouseItemId { get; set; }

    [Required, MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ProductNumber { get; set; }

    [MaxLength(100)]
    public string? Barcode { get; set; }

    [Required, MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public int QuantityChange { get; set; }
    public decimal UnitPrice { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    [Required, MaxLength(256)]
    public string UserEmail { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
