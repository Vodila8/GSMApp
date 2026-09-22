using gsm.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace gsm.Areas.Identity.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ReturnUrl { get; set; } = string.Empty;

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByEmailAsync(Input.Login)
                ?? await _userManager.Users.FirstOrDefaultAsync(item => item.PhoneNumber == Input.Login);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return Page();
            }

            if (user.Status == UserStatus.PendingApproval)
            {
                ModelState.AddModelError(string.Empty, "Your registration is pending admin approval.");
                return Page();
            }

            if (user.Status == UserStatus.Rejected)
            {
                ModelState.AddModelError(string.Empty, "Your registration has been rejected.");
                return Page();
            }

            if (user.Status == UserStatus.Blocked)
            {
                ModelState.AddModelError(string.Empty, "Your account has been blocked.");
                return Page();
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                Input.Password,
                isPersistent: true,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                if (user.Status != UserStatus.Active)
                {
                    await _signInManager.SignOutAsync();
                    ModelState.AddModelError(string.Empty, "Your account is not active yet.");
                    return Page();
                }

                return LocalRedirect(returnUrl);
            }

            if (result.IsNotAllowed)
            {
                ModelState.AddModelError(string.Empty, "Email not confirmed. Please check your inbox.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
            }
        }

        return Page();
    }

    public class InputModel
    {
        [Required]
        [Display(Name = "Email or phone number")]
        public string Login { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}
