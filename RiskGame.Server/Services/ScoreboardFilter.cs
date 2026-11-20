using System;
using RiskGame.Contracts.Scoreboard;

namespace RiskGame.Server.Services
{
    public sealed class ScoreboardFilter
    {
        private const int MinimumLimit = 1;
        private const int MaximumLimit = 100;

        public ScoreboardFilter(ScoreQuery query, int userId)
        {
            UserId = userId;
            FriendsOnly = query.FriendsOnly;
            Descending = query.Descending;
            TopLimit = Math.Clamp(query.TopLimit, MinimumLimit, MaximumLimit);
        }

        public int UserId { get; }

        public bool FriendsOnly { get; }

        public bool Descending { get; }

        public int TopLimit { get; }
    }
}
