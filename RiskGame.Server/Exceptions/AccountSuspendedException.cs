using System;

namespace RiskGame.Exceptions
{
    public sealed class AccountSuspendedException : InvalidSessionException
    {
        public AccountSuspendedException(DateTime bannedUntilUtc) : base("The account is suspended.")
        {
            BannedUntilUtc = DateTime.SpecifyKind(bannedUntilUtc, DateTimeKind.Utc);
        }

        public DateTime BannedUntilUtc { get; }
    }
}
