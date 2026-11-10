using System;

namespace RiskGame.Server.Services
{
    public sealed class RegisteredSession
    {
        public int UserId { get; init; }

        public DateTime ExpiresAt { get; init; }
    }
}
