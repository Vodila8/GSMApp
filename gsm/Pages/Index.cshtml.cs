using gsm.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ApplicationDbContext _dbContext;

        public IndexModel(ILogger<IndexModel> logger, ApplicationDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public bool ShowOrderDashboard => User.IsInRole("Administrator") || User.IsInRole("Boss") || User.IsInRole("Technician");
        public OrderDashboardStats OrderStats { get; private set; } = new();
        public List<WarehousePreviewItem> WarehouseItems { get; private set; } = [];

        [BindProperty(SupportsGet = true)]
        public string? WarehouseSearch { get; set; }

        public async Task OnGetAsync()
        {
            if (!ShowOrderDashboard) return;

            var today = DateOnly.FromDateTime(DateTime.Now);
            var orders = _dbContext.ServiceOrders.AsQueryable();
            var total = await orders.CountAsync();
            var completed = await orders.CountAsync(order => order.Stages.Any(stage =>
                stage.IsFixed && stage.Name == "End" && stage.EndDate.HasValue && stage.EndDate <= today));

            OrderStats = new OrderDashboardStats
            {
                Total = total,
                Completed = completed,
                InProgress = total - completed
            };

            WarehouseItems = await _dbContext.WarehouseItems
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .Select(item => new WarehousePreviewItem
                {
                    Id = item.Id,
                    PartName = item.PartName,
                    ProductNumber = item.ProductNumber ?? item.Id.ToString(),
                    Barcode = item.Barcode,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity
                })
                .ToListAsync();
        }

        public class WarehousePreviewItem
        {
            public int Id { get; set; }
            public string PartName { get; set; } = string.Empty;
            public string ProductNumber { get; set; } = string.Empty;
            public string? Barcode { get; set; }
            public decimal UnitPrice { get; set; }
            public int Quantity { get; set; }
        }

        public class OrderDashboardStats
        {
            public int Total { get; set; }
            public int InProgress { get; set; }
            public int Completed { get; set; }
        }
    }
}
