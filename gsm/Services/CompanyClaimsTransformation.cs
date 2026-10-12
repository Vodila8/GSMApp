using System.Security.Claims;
using gsm.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace gsm.Services;

public class CompanyClaimsTransformation : IClaimsTransformation
{
    private readonly UserManager<ApplicationUser> _userManager;

    public CompanyClaimsTransformation(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true || principal.HasClaim(claim => claim.Type == TenantContext.CompanyClaimType))
            return principal;

        var user = await _userManager.GetUserAsync(principal);
        if (!string.IsNullOrWhiteSpace(user?.CompanyId) && principal.Identity is ClaimsIdentity identity)
            identity.AddClaim(new Claim(TenantContext.CompanyClaimType, user.CompanyId));

        return principal;
    }
}
