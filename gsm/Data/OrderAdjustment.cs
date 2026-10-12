using System.ComponentModel.DataAnnotations;

namespace gsm.Data;

public class OrderAdjustment
{
    public int Id { get; set; }

    public string CompanyId { get; set; } = string.Empty;

    public int ServiceOrderId { get; set; }

    public ServiceOrder ServiceOrder { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
