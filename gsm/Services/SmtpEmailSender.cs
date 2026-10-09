using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text.RegularExpressions;

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

        var fromName = _configuration["Smtp:FromName"] ?? "GSM Service Center";
        var replyTo = _configuration["Smtp:ReplyTo"];
        var plainTextMessage = Regex.Replace(htmlMessage, "<[^>]+>", " ");
        plainTextMessage = WebUtility.HtmlDecode(Regex.Replace(plainTextMessage, @"\s+", " ")).Trim();

        using var message = new MailMessage
        {
            From = new MailAddress(username, fromName),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true,
            SubjectEncoding = System.Text.Encoding.UTF8,
            BodyEncoding = System.Text.Encoding.UTF8,
            HeadersEncoding = System.Text.Encoding.UTF8
        };
        message.To.Add(new MailAddress(email));
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            message.ReplyToList.Add(new MailAddress(replyTo));
        }

        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            plainTextMessage,
            System.Text.Encoding.UTF8,
            MediaTypeNames.Text.Plain));
        message.Headers.Add("Auto-Submitted", "auto-generated");
        message.Headers.Add("X-Auto-Response-Suppress", "All");

        _logger.LogInformation("Sending email to {Email} via {Host}:{Port}", email, host, port);
        await client.SendMailAsync(message);
    }
}
