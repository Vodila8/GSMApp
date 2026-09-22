using gsm.Data;
using gsm.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.Net;
using System.Text;
using System.Text.Json;

namespace gsm.Pages.Admin;

public class ApproveUserModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IDataProtector _protector;
    private readonly ILogger<ApproveUserModel> _logger;

    public ApproveUserModel(
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<ApproveUserModel> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _protector = dataProtectionProvider.CreateProtector("gsm.AdminApproval");
        _logger = logger;
    }

    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string? payload, bool? approve)
    {
        if (string.IsNullOrWhiteSpace(payload) || approve == null)
        {
            StatusMessage = "Invalid approval link.";
            return Page();
        }

        try
        {
            var protectedBytes = WebEncoders.Base64UrlDecode(payload);
            var unprotected = _protector.Unprotect(protectedBytes);
            var data = JsonSerializer.Deserialize<ApprovalPayload>(Encoding.UTF8.GetString(unprotected));

            if (data == null || string.IsNullOrEmpty(data.UserId))
            {
                StatusMessage = "Invalid approval link.";
                return Page();
            }

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - data.Ts > TimeSpan.FromDays(7).TotalSeconds)
            {
                StatusMessage = "Approval link has expired.";
                return Page();
            }

            var user = await _userManager.FindByIdAsync(data.UserId);
            if (user == null)
            {
                StatusMessage = "User not found.";
                return Page();
            }

            // PendingEmailConfirmation is accepted here as well so an administrator can
            // safely resend a confirmation message for registrations that got stuck before
            // the email was delivered.
            if (user.Status != UserStatus.PendingApproval && user.Status != UserStatus.PendingEmailConfirmation)
            {
                StatusMessage = "This user has already been processed.";
                return Page();
            }

            if (approve == true)
            {
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, code, returnUrl = Url.Content("~/") },
                    protocol: Request.Scheme);

                try
                {
                    await _emailSender.SendEmailAsync(
                        user.Email!,
                        "Confirm your registration",
                        $"Hello <b>{WebUtility.HtmlEncode(user.UserName)}</b>,<br><br>" +
                        $"Your registration has been approved. Please confirm your email by <a href='{WebUtility.HtmlEncode(callbackUrl)}'>clicking here</a>.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not send registration confirmation email to {Email}", user.Email);
                    StatusMessage = "Confirmation email could not be sent. See the server log and try the approval link again.";
                    return Page();
                }

                if (!await _userManager.IsInRoleAsync(user, "Boss"))
                {
                    var addRoleResult = await _userManager.AddToRoleAsync(user, "Boss");
                    if (!addRoleResult.Succeeded)
                    {
                        _logger.LogError("Confirmation email was sent to {Email}, but the Boss role could not be assigned: {Errors}",
                            user.Email, string.Join("; ", addRoleResult.Errors.Select(error => error.Description)));
                        StatusMessage = "Confirmation email was sent, but the Boss role could not be assigned. See the server log.";
                        return Page();
                    }
                }

                // Do this only after the message has left the application. If sending fails,
                // the same approval link remains usable for a retry.
                user.Status = UserStatus.PendingEmailConfirmation;
                user.ApprovedAt = DateTime.UtcNow;
                user.ApprovedById = "email-approval";
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    _logger.LogError("Confirmation email was sent to {Email}, but the user status could not be updated: {Errors}",
                        user.Email, string.Join("; ", updateResult.Errors.Select(error => error.Description)));
                    StatusMessage = "Confirmation email was sent, but the account status could not be updated. See the server log.";
                    return Page();
                }

                StatusMessage = "Boss approved and confirmation email sent.";
            }
            else
            {
                if (user.Status != UserStatus.PendingApproval)
                {
                    StatusMessage = "This user has already been approved. Use the approval link to resend the confirmation email.";
                    return Page();
                }

                user.Status = UserStatus.Rejected;
                await _userManager.UpdateAsync(user);

                await _emailSender.SendEmailAsync(
                    user.Email!,
                    "Registration rejected",
                    "Hello,<br><br>Your registration was not approved.");

                StatusMessage = "User rejected.";
            }
        }
        catch
        {
            StatusMessage = "Invalid or corrupted approval link.";
        }

        return Page();
    }

    public record ApprovalPayload(string UserId, string Email, long Ts);
}
