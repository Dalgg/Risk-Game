using System;

namespace RiskGame.Server.Services
{
    public sealed class RegisteredSession
    {
        public int UserId { get; init; }

        public DateTime ExpiresAt { get; init; }

        public string ConnectionId { get; set; } = string.Empty;

        public long DisconnectedTimestamp { get; set; }

        public DateTime? BannedUntil { get; set; }
    }
}
