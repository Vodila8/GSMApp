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

        BossEmail = await _dbContext.Users
            .IgnoreQueryFilters()
            .Where(user => user.CompanyId == Order.CompanyId &&
                           _dbContext.UserRoles.Any(userRole => userRole.UserId == user.Id &&
                               _dbContext.Roles.Any(role => role.Id == userRole.RoleId && role.Name == "Boss")))
            .Select(user => user.Email)
            .FirstOrDefaultAsync() ?? "support unavailable";

        return Page();
    }
}
