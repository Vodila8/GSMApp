using System.ComponentModel.DataAnnotations;

namespace gsm.Data;

public enum BusinessPartnerConnectionStatus
{
    Pending,
    Accepted,
    Rejected
}

public class BusinessPartnerConnection
{
    public int Id { get; set; }

    public string OwnerCompanyId { get; set; } = string.Empty;
    public string PartnerCompanyId { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string InvitedEmail { get; set; } = string.Empty;

    [Required, MaxLength(450)]
    public string InvitedByUserId { get; set; } = string.Empty;

    public BusinessPartnerConnectionStatus Status { get; set; } = BusinessPartnerConnectionStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }

    public Company? OwnerCompany { get; set; }
    public Company? PartnerCompany { get; set; }
    public ICollection<ServiceOrder> ServiceOrders { get; set; } = [];
}
