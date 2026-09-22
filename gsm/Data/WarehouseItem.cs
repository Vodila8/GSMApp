using System.ComponentModel.DataAnnotations;

namespace gsm.Data;

public class WarehouseItem
{
    public string CompanyId { get; set; } = string.Empty;

    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string PartName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ProductNumber { get; set; }

    [MaxLength(100)]
    public string? Barcode { get; set; }

    [Range(0, 999999.99)]
    public decimal UnitPrice { get; set; }

    [Range(0, 999999.99)]
    public decimal DeliveryPrice { get; set; }

    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
