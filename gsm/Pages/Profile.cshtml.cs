using gsm.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IWebHostEnvironment _environment;

    public ProfileModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _signInManager = signInManager;
        _environment = environment;
    }

    public string Email { get; private set; } = string.Empty;

    [BindProperty]
    public string Confirmation { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        Email = user?.Email ?? User.Identity?.Name ?? string.Empty;
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

        Email = user.Email ?? User.Identity?.Name ?? string.Empty;
        if (!string.Equals(Confirmation?.Trim(), "DELETE", StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(Confirmation), "Type DELETE to confirm account deletion.");
            return Page();
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        var deletedPhotoPaths = new List<string>();
        var orders = await _dbContext.ServiceOrders
            .Where(order => order.CustomerId == user.Id)
            .ToListAsync();
        foreach (var order in orders)
        {
            order.CustomerId = null;
            order.CustomerDeviceId = null;
            order.Customer = null;
            order.CustomerDevice = null;
        }

        var devices = await _dbContext.CustomerDevices
            .Include(device => device.Photos)
            .Where(device => device.CustomerId == user.Id)
            .ToListAsync();
        var memberships = await _dbContext.UserCompanyMemberships
            .IgnoreQueryFilters()
            .Where(membership => membership.UserId == user.Id)
            .ToListAsync();
        _dbContext.UserCompanyMemberships.RemoveRange(memberships);
        foreach (var device in devices)
        {
            foreach (var photo in device.Photos)
            {
                deletedPhotoPaths.Add(Path.Combine(_environment.WebRootPath, "uploads", "customer-devices", photo.FileName));
            }
        }
        _dbContext.CustomerDevices.RemoveRange(devices);

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync();
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        await transaction.CommitAsync();
        await _signInManager.SignOutAsync();
        foreach (var path in deletedPhotoPaths)
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }

        return RedirectToPage("/Account/Login", new { area = "Identity" });
    }
}
