using System.ComponentModel.DataAnnotations;

namespace gsm.Data;

public class CustomerDevicePhoto
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string CompanyId { get; set; } = string.Empty;

    public int CustomerDeviceId { get; set; }
    public CustomerDevice CustomerDevice { get; set; } = null!;

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? OriginalFileName { get; set; }

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
