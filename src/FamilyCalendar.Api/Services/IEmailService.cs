namespace FamilyCalendar.Api.Services;

/// <summary>
/// Service for sending email notifications.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an invitation email to a user.
    /// </summary>
    /// <param name="toEmail">The recipient's email address.</param>
    /// <param name="tenantName">The name of the tenant they're being invited to.</param>
    /// <param name="inviterName">The name of the person sending the invitation.</param>
    /// <param name="acceptUrl">The URL to accept the invitation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendInvitationEmailAsync(string toEmail, string tenantName, string inviterName, string acceptUrl);
}
