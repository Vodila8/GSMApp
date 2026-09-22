using System.ComponentModel.DataAnnotations;

namespace gsm.Data;

public class OrderStage
{
    public int Id { get; set; }

    public string CompanyId { get; set; } = string.Empty;

    public int ServiceOrderId { get; set; }

    public ServiceOrder ServiceOrder { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool IsFixed { get; set; }
}
