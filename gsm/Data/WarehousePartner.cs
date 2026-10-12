using System.ComponentModel.DataAnnotations;

namespace gsm.Data;

public class WarehousePartner
{
    public int Id { get; set; }

    public string CompanyId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Egn { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(100)]
    public string? Bulstat { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WarehouseItem> WarehouseItems { get; set; } = [];
}
