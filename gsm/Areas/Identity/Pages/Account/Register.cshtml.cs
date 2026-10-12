using gsm.Data;
using gsm.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.DataProtection;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;

namespace gsm.Areas.Identity.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ApplicationDbContext _dbContext;
    private readonly IDataProtector _protector;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        ApplicationDbContext dbContext,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<RegisterModel> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _dbContext = dbContext;
        _protector = dataProtectionProvider.CreateProtector("gsm.AdminApproval");
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ReturnUrl { get; set; } = string.Empty;
    public string? StatusMessage { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (ModelState.IsValid)
        {
            if (await _userManager.FindByEmailAsync(Input.Email) != null)
            {
                ModelState.AddModelError("Input.Email", "An account with this email already exists. Please log in.");
                return Page();
            }

            var company = new Company { Name = Input.CompanyName.Trim() };
            _dbContext.Companies.Add(company);
            await _dbContext.SaveChangesAsync();

            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                CustomerName = Input.OwnerName.Trim(),
                CompanyId = company.Id,
                Status = UserStatus.PendingApproval
            };

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Boss");
                var payload = JsonSerializer.Serialize(new { UserId = user.Id, user.Email, Ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
                var token = WebEncoders.Base64UrlEncode(_protector.Protect(Encoding.UTF8.GetBytes(payload)));
                var approveUrl = Url.Page("/Admin/ApproveUser", null,
                    new { payload = token, approve = true }, Request.Scheme);
                var rejectUrl = Url.Page("/Admin/ApproveUser", null,
                    new { payload = token, approve = false }, Request.Scheme);

                try
                {
                    await _emailSender.SendEmailAsync(
                        "servicefixmaster@gmail.com",
                        "Approve Boss registration",
                        $"New company registration:<br><br><b>Company:</b> {WebUtility.HtmlEncode(company.Name)}<br>" +
                        $"<b>Owner:</b> {WebUtility.HtmlEncode(user.CustomerName)}<br><b>Email:</b> {WebUtility.HtmlEncode(user.Email)}<br><br>" +
                        $"<a href='{WebUtility.HtmlEncode(approveUrl)}' style='padding:8px 16px;background:#198754;color:#fff;text-decoration:none;border-radius:4px;'>Approve Boss</a> " +
                        $"<a href='{WebUtility.HtmlEncode(rejectUrl)}' style='padding:8px 16px;background:#dc3545;color:#fff;text-decoration:none;border-radius:4px;'>Reject</a>");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not send an approval email for Boss registration by {Email}", user.Email);
                    StatusMessage = "Company registration was saved, but the approval request could not be sent. Please contact support.";
                    return Page();
                }

                StatusMessage = "Company registration successful. The Boss account is waiting for approval.";
                return Page();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            _dbContext.Companies.Remove(company);
            await _dbContext.SaveChangesAsync();
        }

        return Page();
    }

    public class InputModel
    {
        [Required, StringLength(200)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;

        [Required, StringLength(200)]
        [Display(Name = "Your Name")]
        public string OwnerName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;
    }
}
