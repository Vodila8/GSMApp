using gsm.Data;
using gsm.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace gsm.Pages;

[Authorize]
public class AcceptTermsModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AcceptTermsModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    public bool Accepted { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public ApplicationUser? UserAccount { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        UserAccount = await _userManager.GetUserAsync(User);
        if (UserAccount == null) return RedirectToPage("/Account/Login", new { area = "Identity" });
        if (User.IsInRole("Boss") || User.IsInRole("Administrator") || User.IsInRole("Technician") || UserAccount.TermsAcceptedAt.HasValue)
            return LocalRedirect(GetSafeReturnUrl());
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        UserAccount = await _userManager.GetUserAsync(User);
        if (UserAccount == null) return RedirectToPage("/Account/Login", new { area = "Identity" });
        if (!Accepted)
        {
            ModelState.AddModelError(nameof(Accepted), "You must accept the service terms to continue.");
            return Page();
        }

        UserAccount.TermsAcceptedAt = DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(UserAccount);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        return LocalRedirect(GetSafeReturnUrl());
    }

    private string GetSafeReturnUrl() =>
        !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl
            : Url.Content("~/MyOrders")!;
}
