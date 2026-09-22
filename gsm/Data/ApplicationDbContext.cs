using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using gsm.Services;

namespace gsm.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly TenantContext _tenantContext;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        TenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<WarehouseItem> WarehouseItems => Set<WarehouseItem>();

    public DbSet<WarehousePartner> WarehousePartners => Set<WarehousePartner>();

    public DbSet<WarehouseSale> WarehouseSales => Set<WarehouseSale>();

    public DbSet<WarehouseAuditEntry> WarehouseAuditEntries => Set<WarehouseAuditEntry>();

    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();

    public DbSet<ServiceOrderLine> ServiceOrderLines => Set<ServiceOrderLine>();

    public DbSet<CustomerDevice> CustomerDevices => Set<CustomerDevice>();

    public DbSet<OrderStage> OrderStages => Set<OrderStage>();

    public DbSet<OrderAdjustment> OrderAdjustments => Set<OrderAdjustment>();

    public DbSet<Company> Companies => Set<Company>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<WarehouseItem>().HasQueryFilter(item => !_tenantContext.IsAuthenticated || item.CompanyId == _tenantContext.CompanyId);
        builder.Entity<WarehousePartner>().HasQueryFilter(partner => !_tenantContext.IsAuthenticated || partner.CompanyId == _tenantContext.CompanyId);
        builder.Entity<WarehouseSale>().HasQueryFilter(sale => !_tenantContext.IsAuthenticated || sale.CompanyId == _tenantContext.CompanyId);
        builder.Entity<WarehouseAuditEntry>().HasQueryFilter(item => !_tenantContext.IsAuthenticated || item.CompanyId == _tenantContext.CompanyId);
        builder.Entity<CustomerDevice>().HasQueryFilter(item => !_tenantContext.IsAuthenticated || item.CompanyId == _tenantContext.CompanyId);
        builder.Entity<ServiceOrder>().HasQueryFilter(item => !_tenantContext.IsAuthenticated || item.CompanyId == _tenantContext.CompanyId);
        builder.Entity<ServiceOrderLine>().HasQueryFilter(item => !_tenantContext.IsAuthenticated || item.CompanyId == _tenantContext.CompanyId);
        builder.Entity<OrderStage>().HasQueryFilter(item => !_tenantContext.IsAuthenticated || item.CompanyId == _tenantContext.CompanyId);
        builder.Entity<OrderAdjustment>().HasQueryFilter(item => !_tenantContext.IsAuthenticated || item.CompanyId == _tenantContext.CompanyId);
    }
}
