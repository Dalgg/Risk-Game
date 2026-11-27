namespace RiskGame.Server.Services
{
    public sealed class AccountSessionIdentity
    {
        public AccountSessionIdentity(int userId, string sessionToken)
        {
            UserId = userId;
            SessionToken = sessionToken;
        }

        public int UserId { get; }

        public string SessionToken { get; }
    }
}
