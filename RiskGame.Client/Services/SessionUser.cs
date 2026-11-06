namespace RiskGame.Services
{
    public sealed record SessionUser(int IdUser, string Username, string Nickname, string? AvatarReference)
    {
        private const int MinimumValidRegisteredUserId = 1;

        public bool IsGuest => IdUser < MinimumValidRegisteredUserId;
    }
}