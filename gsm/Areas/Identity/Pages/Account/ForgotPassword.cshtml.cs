using gsm.Data;
using gsm.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;

namespace gsm.Areas.Identity.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(UserManager<ApplicationUser> userManager, IEmailSender emailSender, ILogger<ForgotPasswordModel> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool EmailSent { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user != null && await _userManager.IsEmailConfirmedAsync(user))
        {
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page("/Account/ResetPassword", null,
                new { area = "Identity", code = encodedCode, email = user.Email }, Request.Scheme);

            try
            {
                await _emailSender.SendEmailAsync(
                    user.Email!,
                    "Reset your password",
                    $"Hello,<br><br>Click <a href='{WebUtility.HtmlEncode(callbackUrl)}'>here</a> to set a new password. " +
                    "This link will expire according to the application's password reset policy.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send a password reset email to {Email}", user.Email);
            }
        }

        EmailSent = true;
        return Page();
    }

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
