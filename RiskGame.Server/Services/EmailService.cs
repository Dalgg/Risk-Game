using System;
using System.Net;
using System.Net.Mail;
using RiskGame.Exceptions;
using EmailStrings = RiskGame.Server.Resources.Strings;

namespace RiskGame.Server.Services
{
    public sealed class EmailService : IEmailDeliveryService
    {
        private const string Host = "smtp.gmail.com";
        private const int Port = 587;
        private const int TimeoutMilliseconds = 10000;
        private const string UserVariable = "RISKGAME_SMTP_USER";
        private const string PasswordVariable = "RISKGAME_SMTP_PASSWORD";

        public void SendVerification(string recipientEmail, string verificationCode)
        {
            string body = string.Format(EmailStrings.VerificationBody, verificationCode);
            SendMessage(recipientEmail, EmailStrings.VerificationSubject, body,
                "Could not deliver the verification email.");
        }

        public void SendPasswordReset(string recipientEmail, string resetCode)
        {
            string body = string.Format(EmailStrings.PasswordResetBody, resetCode);
            SendMessage(recipientEmail, EmailStrings.PasswordResetSubject, body,
                "Could not deliver the password reset email.");
        }

        private static void SendMessage(string recipientEmail, string subject, string body, string deliveryErrorMessage)
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
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                client.Send(mailMessage);
            }
            catch (SmtpException smtpException)
            {
                throw new DataOperationException(deliveryErrorMessage, smtpException);
            }
            catch (FormatException formatException)
            {
                throw new DataOperationException("Invalid email configuration.", formatException);
            }
        }
    }
}
