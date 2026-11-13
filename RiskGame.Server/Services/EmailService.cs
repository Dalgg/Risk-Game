using System;
using System.Net;
using System.Net.Mail;
using RiskGame.Exceptions;
using RiskGame.Services;
using EmailStrings = RiskGame.Server.Resources.Strings;

namespace RiskGame.Server.Services
{
    public sealed class EmailService
    {
        private const string Host = "smtp.gmail.com";
        private const int Port = 587;
        private const int TimeoutMilliseconds = 10000;
        private const string UserVariable = "RISKGAME_SMTP_USER";
        private const string PasswordVariable = "RISKGAME_SMTP_PASSWORD";

        public void SendVerification(string recipientEmail, string verificationCode)
        {
            string? user = Environment.GetEnvironmentVariable(UserVariable);
            string? password = Environment.GetEnvironmentVariable(PasswordVariable);
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            {
                throw new DataOperationException("Gmail SMTP credentials are not configured on the server.");
            }
            try
            {
                using SmtpClient client = new SmtpClient(Host, Port);
                client.EnableSsl = true;
                client.Timeout = TimeoutMilliseconds;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(user, password);
                using MailMessage mailMessage = new MailMessage(user, recipientEmail);
                mailMessage.Subject = EmailStrings.VerificationSubject;
                mailMessage.Body = string.Format(EmailStrings.VerificationBody, verificationCode);
                client.Send(mailMessage);
            }
            catch (SmtpException smtpException)
            {
                JsonLogger.Error(smtpException, "Could not deliver the registration verification email.");
                throw new DataOperationException("Could not deliver the verification email.", smtpException);
            }
            catch (FormatException formatException)
            {
                JsonLogger.Error(formatException, "The verification email configuration is invalid.");
                throw new DataOperationException("Invalid verification email configuration.", formatException);
            }
        }
    }
}
