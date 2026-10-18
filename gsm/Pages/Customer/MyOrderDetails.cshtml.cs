using System.Security.Claims;
using gsm.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages;

[Authorize]
public class MyOrderDetailsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public MyOrderDetailsModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public ServiceOrder? Order { get; private set; }
    public string BossEmail { get; private set; } = "support unavailable";
    public string BossPhone { get; private set; } = "support unavailable";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Order = await _dbContext.ServiceOrders
            .IgnoreQueryFilters()
            .Include(order => order.Customer)
            .Include(order => order.Lines)
            .Include(order => order.Stages.OrderBy(stage => stage.SortOrder))
            .FirstOrDefaultAsync(order => order.Id == id);

        if (Order == null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!User.IsInRole("Boss") && !User.IsInRole("Administrator") && !User.IsInRole("Technician") && Order.CustomerId != userId)
        {
            return Forbid();
        }

        var staffContacts = await (
            from user in _dbContext.Users.IgnoreQueryFilters()
            join userRole in _dbContext.UserRoles on user.Id equals userRole.UserId
            join role in _dbContext.Roles on userRole.RoleId equals role.Id
            where (user.CompanyId == Order.CompanyId ||
                   _dbContext.UserCompanyMemberships.IgnoreQueryFilters().Any(membership =>
                       membership.UserId == user.Id && membership.CompanyId == Order.CompanyId)) &&
                  (role.NormalizedName == "BOSS" || role.NormalizedName == "ADMINISTRATOR" || role.NormalizedName == "TECHNICIAN" ||
                   role.Name == "Boss" || role.Name == "Administrator" || role.Name == "Technician")
            select new { user.Email, user.PhoneNumber, Role = role.NormalizedName ?? role.Name }
        ).ToListAsync();

        var orderedContacts = staffContacts
            .OrderBy(contact => string.Equals(contact.Role, "BOSS", StringComparison.OrdinalIgnoreCase) ? 0 :
                                string.Equals(contact.Role, "ADMINISTRATOR", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .ToList();

        BossEmail = orderedContacts.FirstOrDefault(contact => !string.IsNullOrWhiteSpace(contact.Email))?.Email
            ?? "support unavailable";
        BossPhone = orderedContacts.FirstOrDefault(contact => !string.IsNullOrWhiteSpace(contact.PhoneNumber))?.PhoneNumber
            ?? "support unavailable";

        return Page();
    }
}
