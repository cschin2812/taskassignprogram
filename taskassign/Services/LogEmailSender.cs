namespace taskassign.Services;

public class LogEmailSender : IEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(ILogger<LogEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string subject, string body)
    {
        _logger.LogInformation("[EMAIL] To: {To}; Subject: {Subject}; Body: {Body}", toEmail, subject, body);
        return Task.CompletedTask;
    }
}
