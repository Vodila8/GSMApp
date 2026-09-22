using Microsoft.AspNetCore.Identity;

namespace gsm.Data;

public class ApplicationUser : IdentityUser
{
    public UserStatus Status { get; set; } = UserStatus.PendingEmailConfirmation;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ApprovedAt { get; set; }

    public string? ApprovedById { get; set; }

    public string? CustomerName { get; set; }

    public string? CompanyId { get; set; }

    public string? Device { get; set; }

    public string? DeviceModelAndSerialNumber { get; set; }
}

public enum UserStatus
{
    PendingEmailConfirmation,
    PendingApproval,
    Active,
    Rejected,
    Blocked
}
