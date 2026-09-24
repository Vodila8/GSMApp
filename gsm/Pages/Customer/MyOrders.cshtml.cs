using System.Security.Claims;
using gsm.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages;

[Authorize]
public class MyOrdersModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public MyOrdersModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<ServiceOrder> Orders { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _dbContext.ServiceOrders
            .IgnoreQueryFilters()
            .Include(order => order.Stages)
            .OrderByDescending(order => order.CreatedAt)
            .AsQueryable();

        if (!User.IsInRole("Boss") && !User.IsInRole("Administrator") && !User.IsInRole("Technician"))
        {
            query = query.Where(order => order.CustomerId == userId);
        }

        Orders = await query.ToListAsync();
    }
}
