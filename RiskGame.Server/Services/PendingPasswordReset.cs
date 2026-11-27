using System;

namespace RiskGame.Server.Services
{
    public sealed class PendingPasswordReset
    {
        public int UserId { get; init; }

        public string Email { get; init; } = string.Empty;

        public byte[] CodeHash { get; set; } = Array.Empty<byte>();

        public long CreatedTimestamp { get; init; }

        public long LastSentTimestamp { get; set; }

        public int FailedAttempts { get; set; }

        public int SentMessages { get; set; }
    }
}
