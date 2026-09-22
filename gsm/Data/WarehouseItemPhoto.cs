using System.ComponentModel.DataAnnotations;

namespace gsm.Data;

public class WarehouseItemPhoto
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string CompanyId { get; set; } = string.Empty;

    public int WarehouseItemId { get; set; }
    public WarehouseItem WarehouseItem { get; set; } = null!;

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? OriginalFileName { get; set; }

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
