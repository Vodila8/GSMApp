namespace gsm.Services;

public class DummyEmailSender : IEmailSender
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DummyEmailSender> _logger;

    public DummyEmailSender(IWebHostEnvironment env, ILogger<DummyEmailSender> logger)
    {
        _env = env;
        _logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var emailsDir = Path.Combine(_env.ContentRootPath, "emails");
        Directory.CreateDirectory(emailsDir);

        var fileName = $"{email}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.html";
        var filePath = Path.Combine(emailsDir, fileName);

        var content = $"<h1>{subject}</h1>{htmlMessage}";
        File.WriteAllText(filePath, content);

        _logger.LogInformation("Email to {Email} saved to {FilePath}", email, filePath);

        return Task.CompletedTask;
    }
}
