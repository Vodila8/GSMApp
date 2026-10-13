using gsm.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gsm.Pages;

[Authorize(Roles = "Boss,Administrator,Technician")]
public class OrderTrackingModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public OrderTrackingModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty]
    public List<StageInput> Stages { get; set; } = [];

    [BindProperty]
    public int WarrantyMonths { get; set; } = 12;

    public ServiceOrder? Order { get; private set; }
    public Company? Company { get; private set; }
    public DateOnly? FinalEndDate => Order?.Stages
        .FirstOrDefault(stage => stage.IsFixed && stage.Name == "End")?.EndDate;

    public bool OpenWarranty { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, bool openWarranty = false)
    {
        OpenWarranty = openWarranty;
        if (!await LoadOrderAsync(id))
        {
            return NotFound();
        }

        WarrantyMonths = Order.WarrantyMonths ?? 12;
        Stages = Order!.Stages.Select(stage => new StageInput
        {
            Id = stage.Id,
            Name = stage.Name,
            StartDate = stage.StartDate,
            EndDate = stage.EndDate,
            IsFixed = stage.IsFixed
        }).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(int id)
    {
        if (!ValidateDates())
        {
            await LoadOrderAsync(id);
            return Page();
        }

        var companyId = await _dbContext.ServiceOrders
            .Where(order => order.Id == id)
            .Select(order => order.CompanyId)
            .SingleOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(companyId)) return NotFound();

        var databaseStages = await _dbContext.OrderStages
            .Where(stage => stage.ServiceOrderId == id)
            .ToDictionaryAsync(stage => stage.Id);

        var submittedExistingIds = Stages.Where(stage => stage.Id > 0).Select(stage => stage.Id).ToHashSet();
        if (Stages.Where(stage => stage.Id > 0).Any(stage => !databaseStages.ContainsKey(stage.Id)))
        {
            return BadRequest();
        }

        var missingFixedStage = databaseStages.Values.Any(stage => stage.IsFixed && !submittedExistingIds.Contains(stage.Id));
        if (missingFixedStage)
        {
            return BadRequest();
        }

        foreach (var deletedStage in databaseStages.Values.Where(stage => !submittedExistingIds.Contains(stage.Id)))
        {
            _dbContext.OrderStages.Remove(deletedStage);
        }

        for (var index = 0; index < Stages.Count; index++)
        {
            var input = Stages[index];
            var stage = input.Id > 0
                ? databaseStages[input.Id]
                : new OrderStage { CompanyId = companyId, ServiceOrderId = id, IsFixed = false };
            if (stage.EndDate != input.EndDate)
            {
                stage.CompletionNotificationSent = false;
            }
            stage.StartDate = input.StartDate;
            stage.EndDate = input.EndDate;
            stage.SortOrder = index;
            stage.Name = input.Name.Trim();

            if (input.Id == 0)
            {
                _dbContext.OrderStages.Add(stage);
            }
        }

        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = "Order stages have been updated.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSaveWarrantyAsync(int id)
    {
        if (WarrantyMonths < 1 || WarrantyMonths > 120)
        {
            ModelState.AddModelError(nameof(WarrantyMonths), "Warranty period must be between 1 and 120 months.");
            await LoadOrderAsync(id);
            return Page();
        }

        if (!await LoadOrderAsync(id) || !FinalEndDate.HasValue)
            return NotFound();

        Order!.WarrantyStartDate = FinalEndDate.Value;
        Order.WarrantyMonths = WarrantyMonths;
        Order.WarrantyEndDate = FinalEndDate.Value.AddMonths(WarrantyMonths);
        await _dbContext.SaveChangesAsync();
        TempData["StatusMessage"] = "Warranty information has been saved.";
        return RedirectToPage(new { id });
    }

    private bool ValidateDates()
    {
        foreach (var stage in Stages)
        {
            if (string.IsNullOrWhiteSpace(stage.Name))
            {
                ModelState.AddModelError(string.Empty, "Each stage must have a name.");
            }
            if (stage.StartDate.HasValue && stage.EndDate.HasValue && stage.EndDate < stage.StartDate)
            {
                ModelState.AddModelError(string.Empty, $"The end date for {stage.Name} cannot be earlier than its start date.");
            }
        }

        return ModelState.IsValid;
    }

    private async Task<bool> LoadOrderAsync(int id)
    {
        Order = await _dbContext.ServiceOrders
            .Include(order => order.Customer)
            .Include(order => order.Lines)
                .ThenInclude(line => line.WarehouseItem)
            .Include(order => order.Stages.OrderBy(stage => stage.SortOrder))
            .FirstOrDefaultAsync(order => order.Id == id);
        Company = Order == null
            ? null
            : await _dbContext.Companies.FirstOrDefaultAsync(company => company.Id == Order.CompanyId);
        return Order != null;
    }

    public class StageInput
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public bool IsFixed { get; set; }
    }
}
