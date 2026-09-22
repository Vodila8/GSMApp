using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace gsm.Services;

public class RabbitMqEmailWorker : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqEmailWorker> _logger;

    public RabbitMqEmailWorker(IConfiguration configuration, IServiceScopeFactory scopeFactory, ILogger<RabbitMqEmailWorker> logger)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory { Uri = new Uri(_configuration["RabbitMq:Uri"] ?? "amqp://guest:guest@localhost:5672/") };
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync(RabbitMqEmailSender.QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, args) =>
                {
                    try
                    {
                        var message = JsonSerializer.Deserialize<EmailMessage>(Encoding.UTF8.GetString(args.Body.ToArray()));
                        if (message == null) throw new InvalidOperationException("Email message is invalid.");
                        using var scope = _scopeFactory.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<SmtpEmailSender>().SendEmailAsync(message.To, message.Subject, message.HtmlBody);
                        await channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Could not process an email from RabbitMQ.");
                        await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true);
                    }
                };
                await channel.BasicConsumeAsync(RabbitMqEmailSender.QueueName, autoAck: false, consumer, cancellationToken: stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "RabbitMQ is unavailable; email worker will retry in 10 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
