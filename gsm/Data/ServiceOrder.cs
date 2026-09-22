namespace gsm.Data;

public class ServiceOrder
{
    public string CompanyId { get; set; } = string.Empty;

    public int Id { get; set; }

    public string CustomerId { get; set; } = string.Empty;

    public ApplicationUser Customer { get; set; } = null!;

    public int? CustomerDeviceId { get; set; }

    public CustomerDevice? CustomerDevice { get; set; }

    public string? Device { get; set; }

    public string? DeviceModelAndSerialNumber { get; set; }

    public string? ProblemOrRepair { get; set; }

    public string? DeviceConditionAndNotes { get; set; }

    public string? Accessories { get; set; }

    public string? DevicePassword { get; set; }

    public string? AdbDiagnosticReport { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal TotalPrice { get; set; }

    public List<ServiceOrderLine> Lines { get; set; } = [];

    public List<OrderStage> Stages { get; set; } = [];

    public List<OrderAdjustment> Adjustments { get; set; } = [];
}
