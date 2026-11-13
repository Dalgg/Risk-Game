using System;
using RiskGame.Models;

namespace RiskGame.Server.Services
{
    public sealed class PendingRegistration
    {
        public User Account { get; init; } = new User();

        public byte[] CodeHash { get; set; } = Array.Empty<byte>();

        public DateTime ExpiresAt { get; init; }

        public DateTime LastSentAt { get; set; }

        public int FailedAttempts { get; set; }

        public int SentMessages { get; set; }
    }
}
