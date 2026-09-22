using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace gsm.Services;

public class RabbitMqEmailSender : IEmailSender
{
    public const string QueueName = "gsm.email";
    private readonly IConfiguration _configuration;
    private readonly SmtpEmailSender _fallbackEmailSender;
    private readonly ILogger<RabbitMqEmailSender> _logger;

    public RabbitMqEmailSender(IConfiguration configuration, SmtpEmailSender fallbackEmailSender, ILogger<RabbitMqEmailSender> logger)
    {
        _configuration = configuration;
        _fallbackEmailSender = fallbackEmailSender;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        try
        {
            var factory = new ConnectionFactory { Uri = new Uri(_configuration["RabbitMq:Uri"] ?? "amqp://guest:guest@localhost:5672/") };
            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();
            await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false);

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new EmailMessage(email, subject, htmlMessage)));
            await channel.BasicPublishAsync("", QueueName, mandatory: false, new BasicProperties { Persistent = true }, body);
            _logger.LogInformation("Email to {Email} was queued in RabbitMQ.", email);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "RabbitMQ is unavailable. Sending email to {Email} directly.", email);
            await _fallbackEmailSender.SendEmailAsync(email, subject, htmlMessage);
        }
    }
}
