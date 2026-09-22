using System.ComponentModel.DataAnnotations;

namespace gsm.Data;

public class WarehousePartner
{
    public int Id { get; set; }

    public string CompanyId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WarehouseItem> WarehouseItems { get; set; } = [];
}
