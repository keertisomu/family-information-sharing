using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FamilyCalendar.Api.Services;

/// <summary>
/// SMTP-based implementation of IEmailService using MailKit.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendInvitationEmailAsync(string toEmail, string tenantName, string inviterName, string acceptUrl)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _configuration["Smtp:FromName"] ?? "Family Calendar",
                _configuration["Smtp:FromEmail"] ?? throw new InvalidOperationException("SMTP FromEmail not configured")
            ));
            message.To.Add(new MailboxAddress(toEmail, toEmail));
            message.Subject = $"You're invited to join {tenantName}";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                    <html>
                    <body>
                        <h2>You've been invited to join {tenantName}</h2>
                        <p>{inviterName} has invited you to join their family calendar on Family Calendar.</p>
                        <p><a href=""{acceptUrl}"" style=""background-color: #4CAF50; color: white; padding: 14px 20px; text-decoration: none; display: inline-block; border-radius: 4px;"">Accept Invitation</a></p>
                        <p>This invitation will expire in 24 hours.</p>
                        <p>If you did not expect this invitation, you can safely ignore this email.</p>
                    </body>
                    </html>
                ",
                TextBody = $@"
You've been invited to join {tenantName}

{inviterName} has invited you to join their family calendar on Family Calendar.

Accept the invitation by visiting: {acceptUrl}

This invitation will expire in 24 hours.

If you did not expect this invitation, you can safely ignore this email.
                "
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            var host = _configuration["Smtp:Host"] ?? throw new InvalidOperationException("SMTP Host not configured");
            var port = int.Parse(_configuration["Smtp:Port"] ?? "587");
            var username = _configuration["Smtp:Username"];
            var password = _configuration["Smtp:Password"];
            var useSsl = bool.Parse(_configuration["Smtp:UseSsl"] ?? "true");

            // MailHog and test SMTP servers typically use None, production uses StartTls or SslOnConnect
            var secureSocketOptions = useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(host, port, secureSocketOptions);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                await client.AuthenticateAsync(username, password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation(
                "Invitation email sent to {Email} for tenant {TenantName} from {InviterName}",
                toEmail,
                tenantName,
                inviterName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send invitation email to {Email} for tenant {TenantName}",
                toEmail,
                tenantName);
            throw;
        }
    }
}
