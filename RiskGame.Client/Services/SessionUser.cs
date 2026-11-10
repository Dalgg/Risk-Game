namespace RiskGame.Services
{
    public sealed record SessionUser(int IdUser, string Username, string Nickname)
    {
        private const int MinimumValidRegisteredUserId = 1;

        public string? AvatarReference { get; init; }

        public string SessionToken { get; init; } = string.Empty;

        public bool IsGuest => IdUser < MinimumValidRegisteredUserId;
    }
}
