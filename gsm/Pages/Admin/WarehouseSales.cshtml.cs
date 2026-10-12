using gsm.Data;
using gsm.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages;

[Authorize(Roles = "Boss,Administrator,Technician")]
public class WarehouseSalesModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TenantContext _tenantContext;

    public WarehouseSalesModel(ApplicationDbContext dbContext, TenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public List<WarehouseSale> Sales { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Sales = await _dbContext.WarehouseSales
            .Include(sale => sale.Partner)
            .Include(sale => sale.WarehouseItem)
            .OrderByDescending(sale => sale.CreatedAt)
            .ToListAsync();
    }
}
