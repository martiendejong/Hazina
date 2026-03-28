using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

namespace Common.Infrastructure.AspNetCore.Email
{
    /// <summary>
    /// SMTP-based implementation of IEmailSender for ASP.NET Identity
    /// Reads configuration from appsettings.json Smtp section
    /// </summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _config;

        public SmtpEmailSender(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Sends an email using SMTP configuration from appsettings.json
        /// Expected configuration keys:
        /// - Smtp:Host
        /// - Smtp:Port
        /// - Smtp:Username
        /// - Smtp:Password
        /// - Smtp:From
        /// </summary>
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            using var client = new SmtpClient(_config["Smtp:Host"], int.Parse(_config["Smtp:Port"]))
            {
                Credentials = new NetworkCredential(_config["Smtp:Username"], _config["Smtp:Password"]),
                EnableSsl = true
            };

            var mailMessage = new MailMessage(_config["Smtp:From"], email, subject, htmlMessage)
            {
                IsBodyHtml = true
            };

            await client.SendMailAsync(mailMessage);
        }
    }
}
