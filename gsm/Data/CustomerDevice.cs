using System.ComponentModel.DataAnnotations;

namespace gsm.Data;

public class CustomerDevice
{
    public string CompanyId { get; set; } = string.Empty;

    public int Id { get; set; }

    public string CustomerId { get; set; } = string.Empty;

    public ApplicationUser Customer { get; set; } = null!;

    [MaxLength(100)]
    public string DeviceType { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? ModelAndSerialNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
