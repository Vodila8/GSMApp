using System.Net;
using System.Security.Cryptography;
using System.Text;
using gsm.Data;
using gsm.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace gsm.Pages;

[Authorize(Roles = "Boss,Administrator")]
public class CreateUserModel : PageModel
{
    private const string PasswordCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<CreateUserModel> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly TenantContext _tenantContext;

    public CreateUserModel(
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        ILogger<CreateUserModel> logger,
        ApplicationDbContext dbContext,
        TenantContext tenantContext)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var allowedRoles = User.IsInRole("Boss")
            ? new[] { "Administrator", "Technician", "User" }
            : new[] { "Technician", "User" };
        if (!allowedRoles.Contains(Input.Role))
        {
            ModelState.AddModelError("Input.Role", "You cannot create an account with this role.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(_tenantContext.CompanyId)) return Forbid();

        var existingUser = await _userManager.FindByEmailAsync(Input.Email!);
        if (existingUser != null)
        {
            if (!User.IsInRole("Boss") || !string.Equals(Input.Role, "User", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Input.Email", "An account with this email already exists.");
                return Page();
            }

            var alreadyLinked = await _dbContext.UserCompanyMemberships.AnyAsync(membership =>
                membership.UserId == existingUser.Id && membership.CompanyId == _tenantContext.CompanyId);
            if (existingUser.CompanyId == _tenantContext.CompanyId || alreadyLinked)
            {
                ModelState.AddModelError("Input.Email", "This client is already registered in this company.");
                return Page();
            }

            _dbContext.UserCompanyMemberships.Add(new UserCompanyMembership
            {
                UserId = existingUser.Id,
                CompanyId = _tenantContext.CompanyId
            });
            existingUser.EmailConfirmed = true;
            existingUser.Status = UserStatus.Active;
            if (!string.IsNullOrWhiteSpace(Input.CustomerName)) existingUser.CustomerName = Input.CustomerName.Trim();
            if (!string.IsNullOrWhiteSpace(Input.PhoneNumber)) existingUser.PhoneNumber = Input.PhoneNumber.Trim();
            await _dbContext.SaveChangesAsync();
            TempData["StatusMessage"] = "The existing client was linked to this company. No new email confirmation is required.";
            return RedirectToPage();
        }

        var generatedPassword = GeneratePassword();
        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            CustomerName = Input.CustomerName,
            PhoneNumber = Input.PhoneNumber,
            CompanyId = _tenantContext.CompanyId,
            Status = UserStatus.PendingEmailConfirmation
        };

        var createResult = await _userManager.CreateAsync(user, generatedPassword);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, Input.Role);
        if (!addRoleResult.Succeeded)
        {
            _logger.LogError("Could not assign the User role to {Email}", user.Email);
            ModelState.AddModelError(string.Empty, "The user was created, but its access role could not be assigned.");
            return Page();
        }

        var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(emailToken));
        var confirmationUrl = Url.Page(
            "/Account/ConfirmEmail",
            pageHandler: null,
            values: new { area = "Identity", userId = user.Id, code = encodedToken },
            protocol: Request.Scheme);
        var applicationUrl = Url.Page(
            "/Index",
            pageHandler: null,
            values: null,
            protocol: Request.Scheme);

        try
        {
            await _emailSender.SendEmailAsync(
                user.Email!,
                "Confirm your account",
                $"Hello {WebUtility.HtmlEncode(Input.CustomerName ?? "there")},<br><br>" +
                "An account has been created for you. Use the password below to sign in after confirming your email.<br><br>" +
                $"<b>Temporary password:</b> {WebUtility.HtmlEncode(generatedPassword)}<br><br>" +
                $"<a href='{WebUtility.HtmlEncode(confirmationUrl)}' style='padding:8px 16px;background:#0d6efd;color:#fff;text-decoration:none;border-radius:4px;'>Confirm email</a><br><br>" +
                $"<a href='{WebUtility.HtmlEncode(applicationUrl)}' style='padding:8px 16px;background:#198754;color:#fff;text-decoration:none;border-radius:4px;'>Open application</a><br><br>" +
                "If you did not expect this email, you can ignore it.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not send account confirmation email to {Email}", user.Email);
            ModelState.AddModelError(string.Empty, "The account was created, but the confirmation email could not be sent. Please contact the administrator.");
            return Page();
        }

        TempData["StatusMessage"] = "The user was created. A confirmation link and temporary password were sent to the specified email address.";
        return RedirectToPage();
    }

    private static string GeneratePassword()
    {
        var characters = new char[16];
        for (var index = 0; index < characters.Length; index++)
        {
            characters[index] = PasswordCharacters[RandomNumberGenerator.GetInt32(PasswordCharacters.Length)];
        }

        return new string(characters);
    }

    public class InputModel
    {
        [Display(Name = "Customer Name")]
        public string? CustomerName { get; set; }

        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Required]
        [Display(Name = "Role")]
        public string Role { get; set; } = "User";

    }
}
