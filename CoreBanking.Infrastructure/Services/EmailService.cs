using CoreBanking.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace CoreBanking.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendTransactionNotificationAsync(string toEmail, string customerName, string reference, decimal amount, string status, CancellationToken ct = default)
    {
        var subject = $"Transaction {status}: {reference}";
        var body = $@"
            <h2>Transaction Notification</h2>
            <p>Dear {customerName},</p>
            <p>Your transaction has been <strong>{status}</strong>.</p>
            <ul>
                <li>Reference: {reference}</li>
                <li>Amount: NGN {amount:N2}</li>
                <li>Status: {status}</li>
            </ul>
            <p>Thank you for banking with us.</p>";

        await SendAsync(toEmail, subject, body, ct);
    }

    public async Task SendLoanDecisionAsync(string toEmail, string customerName, string loanNumber, string status, string? reason, CancellationToken ct = default)
    {
        var subject = $"Loan Application {status}: {loanNumber}";
        var body = $@"
            <h2>Loan Decision</h2>
            <p>Dear {customerName},</p>
            <p>Your loan application <strong>{loanNumber}</strong> has been <strong>{status}</strong>.</p>
            {(reason != null ? $"<p>Reason: {reason}</p>" : "")}
            <p>Thank you.</p>";

        await SendAsync(toEmail, subject, body, ct);
    }

    public async Task SendDisputeUpdateAsync(string toEmail, string reference, string status, CancellationToken ct = default)
    {
        var subject = $"Dispute Update: {reference}";
        var body = $"Your dispute for transaction {reference} is now <strong>{status}</strong>.";
        await SendAsync(toEmail, subject, body, ct);
    }

    private async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        try
        {
            var host = _config["SMTP_HOST"] ?? "smtp.mailtrap.io";
            var port = int.Parse(_config["SMTP_PORT"] ?? "587");
            var user = _config["SMTP_USER"] ?? "";
            var pass = _config["SMTP_PASS"] ?? "";
            var from = _config["SMTP_FROM"] ?? "noreply@corebanking.local";

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(from));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, ct);
            if (!string.IsNullOrEmpty(user))
                await client.AuthenticateAsync(user, pass, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            // Don't throw – email failure should not break the transaction
        }
    }
}