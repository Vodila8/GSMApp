using gsm.Data;
using gsm.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages;

[Authorize(Roles = "Boss,Administrator,Technician")]
public class OrderOverviewModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TenantContext _tenantContext;

    public OrderOverviewModel(ApplicationDbContext dbContext, TenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public string StatusFilter { get; private set; } = "all";
    public string? OrderNumber { get; private set; }
    public List<OrderListItem> Orders { get; private set; } = [];

    public async Task OnGetAsync(string? status, string? orderNumber)
    {
        StatusFilter = status?.ToLowerInvariant() switch
        {
            "completed" => "completed",
            "progress" => "progress",
            _ => "all"
        };

        OrderNumber = orderNumber?.Trim();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var companyId = _tenantContext.CompanyId;
        var canSeePartnerOrders = User.IsInRole("Boss") || User.IsInRole("Administrator");
        var query = _dbContext.ServiceOrders
            .IgnoreQueryFilters()
            .Where(order => order.CompanyId == companyId ||
                (canSeePartnerOrders && _dbContext.BusinessPartnerConnections.Any(connection =>
                    connection.Id == order.BusinessPartnerConnectionId &&
                    connection.PartnerCompanyId == companyId &&
                    connection.Status == BusinessPartnerConnectionStatus.Accepted)))
            .Include(order => order.Customer)
            .Include(order => order.Stages)
            .OrderByDescending(order => order.CreatedAt)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(OrderNumber))
        {
            var searchTerm = OrderNumber;
            var hasOrderId = int.TryParse(searchTerm, out var orderId);
            query = query.Where(order =>
                (hasOrderId && order.Id == orderId) ||
                (order.CompanyId == companyId && order.Customer != null &&
                 (EF.Functions.ILike(order.Customer.CustomerName ?? string.Empty, $"%{searchTerm}%") ||
                  EF.Functions.ILike(order.Customer.Email ?? string.Empty, $"%{searchTerm}%") ||
                  EF.Functions.ILike(order.Customer.PhoneNumber ?? string.Empty, $"%{searchTerm}%"))));
        }

        if (StatusFilter == "completed")
        {
            query = query.Where(order => order.Stages.Any(stage => stage.IsFixed && stage.Name == "End" && stage.EndDate.HasValue && stage.EndDate <= today));
        }
        else if (StatusFilter == "progress")
        {
            query = query.Where(order => !order.Stages.Any(stage => stage.IsFixed && stage.Name == "End" && stage.EndDate.HasValue && stage.EndDate <= today));
        }

        Orders = await query.Select(order => new OrderListItem
        {
            Id = order.Id,
            CustomerName = order.CompanyId == companyId
                ? order.Customer == null ? "No customer" : order.Customer.CustomerName ?? order.Customer.Email ?? "Unknown customer"
                : "Partner order",
            Device = order.Device ?? "Device",
            DeviceDetails = order.DeviceModelAndSerialNumber,
            CreatedAt = order.CreatedAt,
            TotalPrice = order.TotalPrice,
            IsCompleted = order.Stages.Any(stage => stage.IsFixed && stage.Name == "End" && stage.EndDate.HasValue && stage.EndDate <= today),
            CurrentStage = order.Stages
                .Where(stage => !stage.IsFixed && stage.StartDate.HasValue && stage.StartDate <= today && (!stage.EndDate.HasValue || stage.EndDate >= today))
                .OrderByDescending(stage => stage.SortOrder)
                .Select(stage => stage.Name)
                .FirstOrDefault()
        }).ToListAsync();
    }

    public class OrderListItem
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Device { get; set; } = string.Empty;
        public string? DeviceDetails { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalPrice { get; set; }
        public bool IsCompleted { get; set; }
        public string? CurrentStage { get; set; }
    }
}
