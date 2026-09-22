using System.Net;
using System.Net.Mail;

namespace gsm.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var host = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
        var port = _configuration.GetValue<int?>("Smtp:Port") ?? 587;
        var username = _configuration["Smtp:Username"] ?? "servicefixmaster@gmail.com";
        var password = _configuration["Smtp:Password"]
            ?? throw new InvalidOperationException("Smtp:Password is not configured.");
        var enableSsl = _configuration.GetValue<bool?>("Smtp:EnableSsl") ?? true;

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(username, password),
            EnableSsl = enableSsl
        };

        using var message = new MailMessage(username, email, subject, htmlMessage)
        {
            IsBodyHtml = true
        };

        _logger.LogInformation("Sending email to {Email} via {Host}:{Port}", email, host, port);
        await client.SendMailAsync(message);
    }
}
