using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using gsm.Data;
using gsm.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gsm.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class CustomerLoginModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailSender _emailSender;

    public CustomerLoginModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailSender emailSender)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
    }

    [BindProperty]
    public string CustomerNumber { get; set; } = string.Empty;

    [BindProperty]
    public string Code { get; set; } = string.Empty;

    public bool ShowCodeForm { get; private set; }

    public void OnGet(bool reset = false, bool code = false, string? customerNumber = null)
    {
        CustomerNumber = customerNumber ?? string.Empty;
        ShowCodeForm = code && !reset;
    }

    public async Task<IActionResult> OnPostSendCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomerNumber))
        {
            ModelState.AddModelError(nameof(CustomerNumber), "Enter your Customer ID.");
            return Page();
        }

        var user = await _userManager.Users.FirstOrDefaultAsync(item => item.CustomerNumber == CustomerNumber.Trim());
        if (user == null || string.IsNullOrWhiteSpace(user.Email) || user.Status is UserStatus.Blocked or UserStatus.Rejected)
        {
            ModelState.AddModelError(string.Empty, "We could not send a login code for this Customer ID.");
            return Page();
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var codeHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        TempData["CustomerLoginChallenge"] = JsonSerializer.Serialize(new
        {
            user.Id,
            CodeHash = codeHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        });

        await _emailSender.SendEmailAsync(
            user.Email,
            "Customer login code",
            $"Your one-time login code is: <b>{code}</b><br><br>This code expires in 10 minutes.");

        ShowCodeForm = true;
        TempData["CustomerLoginMessage"] = "A one-time code was sent to your email.";
        return RedirectToPage(new { code = true, customerNumber = CustomerNumber.Trim() });
    }

    public async Task<IActionResult> OnPostVerifyCodeAsync()
    {
        ShowCodeForm = true;
        var challengeJson = TempData.Peek("CustomerLoginChallenge") as string;
        if (string.IsNullOrWhiteSpace(challengeJson) || string.IsNullOrWhiteSpace(Code))
        {
            ModelState.AddModelError(string.Empty, "The code is invalid or expired.");
            return Page();
        }

        var challenge = JsonSerializer.Deserialize<CustomerLoginChallenge>(challengeJson);
        var codeHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Code.Trim())));
        if (challenge == null || challenge.ExpiresAt < DateTimeOffset.UtcNow ||
            !CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(challenge.CodeHash),
                Convert.FromBase64String(codeHash)))
        {
            ModelState.AddModelError(string.Empty, "The code is invalid or expired.");
            return Page();
        }

        var user = await _userManager.FindByIdAsync(challenge.Id);
        if (user == null || user.CustomerNumber != CustomerNumber.Trim() || user.Status is UserStatus.Blocked or UserStatus.Rejected)
        {
            ModelState.AddModelError(string.Empty, "The code is invalid or expired.");
            return Page();
        }

        TempData.Remove("CustomerLoginChallenge");
        await _signInManager.SignInAsync(user, isPersistent: true);
        return Redirect("/MyOrders");
    }

    private sealed record CustomerLoginChallenge(string Id, string CodeHash, DateTimeOffset ExpiresAt);
}
