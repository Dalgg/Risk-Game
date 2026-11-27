namespace RiskGame.Server.Services
{
    public interface IEmailDeliveryService
    {
        void SendVerification(string recipientEmail, string verificationCode);

        void SendPasswordReset(string recipientEmail, string resetCode);
    }
}
