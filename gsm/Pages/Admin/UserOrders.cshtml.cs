using System.ComponentModel.DataAnnotations;
using gsm.Data;
using gsm.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages;

[Authorize(Roles = "Boss,Administrator")]
public class UserOrdersModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserOrdersModel(ApplicationDbContext dbContext, TenantContext tenantContext, UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _userManager = userManager;
    }

    public ApplicationUser? Customer { get; private set; }
    public List<ServiceOrder> Orders { get; private set; } = [];

    [BindProperty]
    public AdjustmentInput Input { get; set; } = new();

    [BindProperty]
    public PasswordInput Password { get; set; } = new();

    public async Task<IActionResult> OnPostSetPasswordAsync(string id)
    {
        if (User.IsInRole("Boss")) return Forbid();
        foreach (var key in ModelState.Keys.Where(key => key.StartsWith(nameof(Input), StringComparison.OrdinalIgnoreCase)).ToList())
            ModelState.Remove(key);
        if (!ModelState.IsValid)
        {
            await LoadAsync(id);
            return Page();
        }

        var user = await _userManager.Users.FirstOrDefaultAsync(item =>
            item.Id == id &&
            (item.CompanyId == _tenantContext.CompanyId || _dbContext.UserCompanyMemberships.Any(membership => membership.UserId == item.Id && membership.CompanyId == _tenantContext.CompanyId)));
        if (user == null) return NotFound();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, Password.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("Password.NewPassword", error.Description);
            await LoadAsync(id);
            return Page();
        }

        TempData["StatusMessage"] = "The user password has been updated.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        return await LoadAsync(id) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostAddAdjustmentAsync(string id)
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync(id);
            return Page();
        }

        var order = await _dbContext.ServiceOrders.FirstOrDefaultAsync(item => item.Id == Input.OrderId && item.CustomerId == id);
        if (order == null)
        {
            return NotFound();
        }

        _dbContext.OrderAdjustments.Add(new OrderAdjustment
        {
            CompanyId = order.CompanyId,
            ServiceOrderId = order.Id,
            Description = Input.Description.Trim(),
            Amount = Input.Amount
        });
        order.TotalPrice += Input.Amount;
        await _dbContext.SaveChangesAsync();

        TempData["StatusMessage"] = "The price item was added to the order.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(string id)
    {
        Customer = await _dbContext.Users.FirstOrDefaultAsync(user =>
            user.Id == id &&
            (user.CompanyId == _tenantContext.CompanyId || _dbContext.UserCompanyMemberships.Any(membership => membership.UserId == user.Id && membership.CompanyId == _tenantContext.CompanyId)));
        if (Customer == null) return false;
        Orders = await _dbContext.ServiceOrders
            .Where(order => order.CustomerId == id)
            .Include(order => order.Adjustments)
            .Include(order => order.Stages)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync();
        return true;
    }

    public class PasswordInput
    {
        [Required, DataType(DataType.Password), Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class AdjustmentInput
    {
        public int OrderId { get; set; }
        [Required, StringLength(200)]
        public string Description { get; set; } = string.Empty;
        [Range(0.01, 999999.99)]
        public decimal Amount { get; set; }
    }
}
