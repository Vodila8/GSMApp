using gsm.Data;
using Microsoft.EntityFrameworkCore;

namespace gsm.Services;

public sealed class OrderStageNotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrderStageNotificationWorker> _logger;

    public OrderStageNotificationWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<OrderStageNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CheckDueStagesAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckDueStagesAsync(stoppingToken);
        }
    }

    private async Task CheckDueStagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var stages = await dbContext.OrderStages
                .Include(stage => stage.ServiceOrder)
                    .ThenInclude(order => order.Customer)
                .Where(stage => !stage.CompletionNotificationSent &&
                                stage.EndDate.HasValue &&
                                stage.EndDate <= today &&
                                stage.ServiceOrder.CustomerId != null &&
                                stage.ServiceOrder.Customer != null &&
                                stage.ServiceOrder.Customer.Email != null)
                .OrderBy(stage => stage.EndDate)
                .Take(100)
                .ToListAsync(cancellationToken);

            foreach (var stage in stages)
            {
                var order = stage.ServiceOrder;
                var customer = order.Customer!;
                var baseUrl = (_configuration["App:PublicUrl"] ?? "https://gsmapp.duckdns.org").TrimEnd('/');
                var orderUrl = $"{baseUrl}/MyOrderDetails/{order.Id}";
                var isEndStage = stage.IsFixed && string.Equals(stage.Name, "End", StringComparison.OrdinalIgnoreCase);
                var subject = isEndStage
                    ? $"Order #{order.Id} is completed"
                    : $"Order #{order.Id} stage completed: {stage.Name}";
                var message = $"Hello {System.Net.WebUtility.HtmlEncode(customer.CustomerName ?? customer.Email!)},<br><br>" +
                              (isEndStage
                                  ? $"Your order <b>#{order.Id}</b> has been completed on <b>{stage.EndDate:dd MMM yyyy}</b>."
                                  : $"The <b>{System.Net.WebUtility.HtmlEncode(stage.Name)}</b> stage of your order <b>#{order.Id}</b> was completed on <b>{stage.EndDate:dd MMM yyyy}</b>.") +
                              $"<br><br><a href='{System.Net.WebUtility.HtmlEncode(orderUrl)}'>Open your order</a><br><br>" +
                              "This is an automatic notification from the service desk.";
                await emailSender.SendEmailAsync(customer.Email!, subject, message);
                stage.CompletionNotificationSent = true;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not process order stage email notifications.");
        }
    }
}
