using System.ComponentModel.DataAnnotations;
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

namespace gsm.Pages;

[Authorize(Roles = "Boss,Administrator")]
public class UsersModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<UsersModel> _logger;

    public UsersModel(
        ApplicationDbContext dbContext,
        TenantContext tenantContext,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        ILogger<UsersModel> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
    }

    [BindProperty]
    public PasswordInput Password { get; set; } = new();

    [BindProperty]
    public CreateUserInput CreateUser { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public List<UserListItem> Users { get; private set; } = [];
    public string? StatusMessage { get; private set; }
    public bool ShowCreateUserModal { get; private set; }

    public async Task<IActionResult> OnPostCreateUserAsync()
    {
        ShowCreateUserModal = true;
        foreach (var key in ModelState.Keys.Where(key => key.Contains(nameof(PasswordInput.NewPassword), StringComparison.OrdinalIgnoreCase)).ToList())
            ModelState.Remove(key);
        _logger.LogInformation("Create user request received for {Email} with role {Role}", CreateUser.Email, CreateUser.Role);
        if (!ModelState.IsValid)
        {
            var validationErrors = string.Join("; ", ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage));
            _logger.LogWarning("Create user validation failed: {ValidationErrors}", validationErrors);
            await OnGetAsync();
            return Page();
        }

        var allowedRoles = User.IsInRole("Boss")
            ? new[] { "Administrator", "Technician", "User" }
            : new[] { "Technician", "User" };
        if (!allowedRoles.Contains(CreateUser.Role))
        {
            ModelState.AddModelError("CreateUser.Role", "You cannot create an account with this role.");
            await OnGetAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(_tenantContext.CompanyId))
        {
            _logger.LogWarning("Create user rejected because the current user has no company claim.");
            return Forbid();
        }
        var existingUser = await _userManager.FindByEmailAsync(CreateUser.Email!);
        if (existingUser != null)
        {
            if (!User.IsInRole("Boss") || !string.Equals(CreateUser.Role, "User", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Create user rejected because the email already exists: {Email}", CreateUser.Email);
                ModelState.AddModelError("CreateUser.Email", "An account with this email already exists.");
                await OnGetAsync();
                return Page();
            }

            var alreadyLinked = await _dbContext.UserCompanyMemberships.AnyAsync(membership =>
                membership.UserId == existingUser.Id && membership.CompanyId == _tenantContext.CompanyId);
            if (existingUser.CompanyId == _tenantContext.CompanyId || alreadyLinked)
            {
                ModelState.AddModelError("CreateUser.Email", "This client is already registered in this company.");
                await OnGetAsync();
                return Page();
            }

            _dbContext.UserCompanyMemberships.Add(new UserCompanyMembership
            {
                UserId = existingUser.Id,
                CompanyId = _tenantContext.CompanyId
            });
            existingUser.EmailConfirmed = true;
            existingUser.Status = UserStatus.Active;
            if (!string.IsNullOrWhiteSpace(CreateUser.CustomerName)) existingUser.CustomerName = CreateUser.CustomerName.Trim();
            if (!string.IsNullOrWhiteSpace(CreateUser.PhoneNumber)) existingUser.PhoneNumber = CreateUser.PhoneNumber.Trim();
            await _dbContext.SaveChangesAsync();
            TempData["StatusMessage"] = "The existing client was linked to this company. No new email confirmation is required.";
            return RedirectToPage();
        }

        var generatedPassword = GeneratePassword();
        var user = new ApplicationUser
        {
            UserName = CreateUser.Email,
            Email = CreateUser.Email,
            CustomerName = CreateUser.CustomerName,
            PhoneNumber = CreateUser.PhoneNumber,
            CompanyId = _tenantContext.CompanyId,
            Status = UserStatus.PendingEmailConfirmation
        };
        var createResult = await _userManager.CreateAsync(user, generatedPassword);
        if (!createResult.Succeeded)
        {
            _logger.LogWarning("Create user failed: {Errors}", string.Join("; ", createResult.Errors.Select(error => error.Description)));
            foreach (var error in createResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
            await OnGetAsync();
            return Page();
        }

        var roleResult = await _userManager.AddToRoleAsync(user, CreateUser.Role);
        if (!roleResult.Succeeded)
        {
            _logger.LogWarning("Create user role assignment failed: {Errors}", string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            ModelState.AddModelError(string.Empty, "The user was created, but the role could not be assigned.");
            await OnGetAsync();
            return Page();
        }

        var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(emailToken));
        var confirmationUrl = Url.Page("/Account/ConfirmEmail", null,
            new { area = "Identity", userId = user.Id, code = encodedToken }, Request.Scheme);
        try
        {
            await _emailSender.SendEmailAsync(user.Email!, "Confirm your account",
                $"Hello {WebUtility.HtmlEncode(CreateUser.CustomerName ?? "there")},<br><br>" +
                "An account has been created for you. Use this temporary password after confirming your email.<br><br>" +
                $"<b>Temporary password:</b> {WebUtility.HtmlEncode(generatedPassword)}<br><br>" +
                $"<a href='{WebUtility.HtmlEncode(confirmationUrl)}'>Confirm email</a>");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not send account confirmation email to {Email}", user.Email);
            ModelState.AddModelError(string.Empty, "The account was created, but the confirmation email could not be sent.");
            await OnGetAsync();
            return Page();
        }

        _logger.LogInformation("User {Email} was created and the confirmation email was sent.", user.Email);
        TempData["StatusMessage"] = "The user was created and a confirmation email was sent.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteUserAsync(string id)
    {
        if (!User.IsInRole("Boss")) return Forbid();
        if (string.IsNullOrWhiteSpace(_tenantContext.CompanyId)) return Forbid();

        var currentUserId = _userManager.GetUserId(User);
        var user = await _userManager.Users.FirstOrDefaultAsync(item => item.Id == id);
        if (user == null) return NotFound();
        if (user.Id == currentUserId) return Forbid();

        var memberships = await _dbContext.UserCompanyMemberships
            .IgnoreQueryFilters()
            .Where(membership => membership.UserId == user.Id)
            .ToListAsync();
        var belongsToCompany = user.CompanyId == _tenantContext.CompanyId || memberships.Any(membership => membership.CompanyId == _tenantContext.CompanyId);
        if (!belongsToCompany) return Forbid();

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Boss", StringComparer.OrdinalIgnoreCase)) return Forbid();

        var currentMembership = memberships.FirstOrDefault(membership => membership.CompanyId == _tenantContext.CompanyId);
        if (currentMembership != null) _dbContext.UserCompanyMemberships.Remove(currentMembership);
        if (user.CompanyId == _tenantContext.CompanyId)
        {
            var replacementCompany = memberships.FirstOrDefault(membership => membership.CompanyId != _tenantContext.CompanyId)?.CompanyId;
            if (replacementCompany != null)
            {
                user.CompanyId = replacementCompany;
                await _dbContext.SaveChangesAsync();
                TempData["StatusMessage"] = $"User {user.Email} was removed from this company.";
                return RedirectToPage();
            }
        }
        if (user.CompanyId != _tenantContext.CompanyId)
        {
            await _dbContext.SaveChangesAsync();
            TempData["StatusMessage"] = $"User {user.Email} was removed from this company.";
            return RedirectToPage();
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            await OnGetAsync();
            return Page();
        }

        _logger.LogInformation("User {Email} was deleted by {AdministratorId}.", user.Email, currentUserId);
        TempData["StatusMessage"] = $"User {user.Email} was deleted.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetPasswordAsync(string id)
    {
        if (User.IsInRole("Boss")) return Forbid();
        foreach (var key in ModelState.Keys.Where(key => key.StartsWith(nameof(CreateUser), StringComparison.OrdinalIgnoreCase)).ToList())
            ModelState.Remove(key);
        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        var user = await _userManager.Users.FirstOrDefaultAsync(item =>
            item.Id == id &&
            (item.CompanyId == _tenantContext.CompanyId || _dbContext.UserCompanyMemberships.Any(membership => membership.UserId == item.Id && membership.CompanyId == _tenantContext.CompanyId)));
        if (user == null)
        {
            return NotFound();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, Password.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            await OnGetAsync();
            return Page();
        }

        StatusMessage = $"Password updated for {user.Email}.";
        Password = new();
        await OnGetAsync();
        return Page();
    }

    public async Task OnGetAsync()
    {
        var hiddenRoles = User.IsInRole("Boss")
            ? new[] { "BOSS" }
            : new[] { "BOSS", "ADMINISTRATOR" };

        var hiddenUserIds = _dbContext.UserRoles.Join(_dbContext.Roles,
            userRole => userRole.RoleId, role => role.Id,
            (userRole, role) => new { userRole.UserId, role.NormalizedName })
            .Where(item => hiddenRoles.Contains(item.NormalizedName!))
            .Select(item => item.UserId);

        var usersQuery = _dbContext.Users
            .Where(user => (user.CompanyId == _tenantContext.CompanyId || _dbContext.UserCompanyMemberships.Any(membership => membership.UserId == user.Id && membership.CompanyId == _tenantContext.CompanyId)) && !hiddenUserIds.Contains(user.Id));
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var search = Search.Trim();
            usersQuery = usersQuery.Where(user =>
                (user.CustomerName != null && EF.Functions.ILike(user.CustomerName, $"%{search}%")) ||
                (user.Email != null && EF.Functions.ILike(user.Email, $"%{search}%")) ||
                (user.PhoneNumber != null && EF.Functions.ILike(user.PhoneNumber, $"%{search}%")));
        }

        var users = await usersQuery
            .OrderBy(user => user.CustomerName ?? user.Email)
            .ToListAsync();

        var roleByUserId = await _dbContext.UserRoles
            .Join(_dbContext.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .Where(item => users.Select(user => user.Id).Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, item => item.Name ?? "User");

        Users = users.Select(user => new UserListItem
        {
            Id = user.Id,
            Name = user.CustomerName ?? user.Email ?? "Unnamed user",
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Role = roleByUserId.GetValueOrDefault(user.Id, "User")
        }).ToList();
    }

    private static string GeneratePassword()
    {
        const string characters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        return new string(Enumerable.Range(0, 16)
            .Select(_ => characters[RandomNumberGenerator.GetInt32(characters.Length)])
            .ToArray());
    }

    public class CreateUserInput
    {
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        [Required, EmailAddress]
        public string? Email { get; set; }
        [Required]
        public string Role { get; set; } = "User";
    }

    public class PasswordInput
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
        [System.ComponentModel.DataAnnotations.Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class UserListItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}
