using RiskGame.Contracts.Scoreboard;
using RiskGame.Server.Services;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class ScoreboardFilterTests
    {
        private const int UserId = 41;
        private const int MinimumLimit = 1;
        private const int MaximumLimit = 100;
        private const int BelowMinimum = -10;
        private const int AboveMaximum = 500;
        private const int ValidLimit = 20;

        [Theory]
        [InlineData(BelowMinimum, MinimumLimit)]
        [InlineData(AboveMaximum, MaximumLimit)]
        [InlineData(ValidLimit, ValidLimit)]
        public void Constructor_RequestedLimit_ClampsToAllowedRange(int requestedLimit, int expectedLimit)
        {
            ScoreQuery query = new ScoreQuery { TopLimit = requestedLimit };

            ScoreboardFilter filter = new ScoreboardFilter(query, UserId);

            Assert.Equal(expectedLimit, filter.TopLimit);
        }
    }
}
