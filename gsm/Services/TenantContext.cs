using System.Security.Claims;

namespace gsm.Services;

public class TenantContext
{
    public const string CompanyClaimType = "gsm_company_id";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
    public string? CompanyId => _httpContextAccessor.HttpContext?.User.FindFirstValue(CompanyClaimType);
}
