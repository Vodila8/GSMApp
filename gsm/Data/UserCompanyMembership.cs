namespace gsm.Data;

public class UserCompanyMembership
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string CompanyId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
