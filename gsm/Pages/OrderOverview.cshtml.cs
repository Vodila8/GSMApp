using gsm.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages;

[Authorize(Roles = "Boss,Administrator,Technician")]
public class OrderOverviewModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public OrderOverviewModel(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public string StatusFilter { get; private set; } = "all";
    public List<OrderListItem> Orders { get; private set; } = [];

    public async Task OnGetAsync(string? status)
    {
        StatusFilter = status?.ToLowerInvariant() switch
        {
            "completed" => "completed",
            "progress" => "progress",
            _ => "all"
        };

        var today = DateOnly.FromDateTime(DateTime.Now);
        var query = _dbContext.ServiceOrders
            .Include(order => order.Customer)
            .Include(order => order.Stages)
            .OrderByDescending(order => order.CreatedAt)
            .AsQueryable();

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
            CustomerName = order.Customer.CustomerName ?? order.Customer.Email ?? "Unknown customer",
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
