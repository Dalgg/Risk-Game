extern alias Client;

using Xunit;
using ProfileMatchHistoryItem = Client::RiskGame.ViewModels.ProfileMatchHistoryItem;
using RecentMatchResult = Client::RiskGame.Contracts.Profile.RecentMatchResult;
using Strings = Client::RiskGame.Resources.Strings;

namespace RiskGame.Server.Tests
{
    public sealed class ProfileMatchHistoryItemTests
    {
        private const int MatchId = 901;
        private const int NoClaims = 0;
        private const string AbandonmentEndReason = "Abandonment";
        private const string CancelledEndReason = "Cancelled";

        [Fact]
        public void HistoryItem_WinningResult_ShowsVictory()
        {
            RecentMatchResult result = new RecentMatchResult { HasResult = true, IsWinner = true };

            ProfileMatchHistoryItem item = new ProfileMatchHistoryItem(result);

            Assert.Equal(Strings.ProfileHistoryVictory, item.OutcomeText);
        }

        [Fact]
        public void HistoryItem_LosingResult_ShowsDefeat()
        {
            RecentMatchResult result = new RecentMatchResult { HasResult = true };

            ProfileMatchHistoryItem item = new ProfileMatchHistoryItem(result);

            Assert.Equal(Strings.ProfileHistoryDefeat, item.OutcomeText);
        }

        [Fact]
        public void HistoryItem_MissingPosition_DoesNotInventOutcome()
        {
            RecentMatchResult result = new RecentMatchResult { IsWinner = true };

            ProfileMatchHistoryItem item = new ProfileMatchHistoryItem(result);

            Assert.Equal(Strings.ProfileHistoryResultUnavailable, item.OutcomeText);
        }

        [Fact]
        public void HistoryItem_MissingScore_ShowsUnavailable()
        {
            ProfileMatchHistoryItem item = new ProfileMatchHistoryItem(new RecentMatchResult());

            Assert.Equal(string.Format(Strings.ProfileHistoryColumnsFormat, Strings.ProfileStatisticUnavailable),
                item.ColumnsText);
        }

        [Fact]
        public void HistoryItem_ZeroClaims_ShowsZero()
        {
            RecentMatchResult result = new RecentMatchResult { HasClaimedColumns = true, ClaimedColumns = NoClaims };

            ProfileMatchHistoryItem item = new ProfileMatchHistoryItem(result);

            Assert.Equal(string.Format(Strings.ProfileHistoryColumnsFormat, NoClaims), item.ColumnsText);
        }

        [Fact]
        public void HistoryItem_Abandonment_DescribesMatchEnd()
        {
            RecentMatchResult result = new RecentMatchResult { EndReason = AbandonmentEndReason };

            ProfileMatchHistoryItem item = new ProfileMatchHistoryItem(result);

            Assert.Equal(Strings.ProfileHistoryAbandonment, item.CompletionText);
        }

        [Fact]
        public void HistoryItem_Cancelled_DoesNotShowVictory()
        {
            RecentMatchResult result = new RecentMatchResult
            {
                EndReason = CancelledEndReason, HasResult = true, IsWinner = true
            };

            ProfileMatchHistoryItem item = new ProfileMatchHistoryItem(result);

            Assert.Equal(Strings.ProfileHistoryCancelled, item.OutcomeText);
        }

        [Fact]
        public void HistoryItem_MatchIdentifier_ShowsIdentifier()
        {
            RecentMatchResult result = new RecentMatchResult { MatchId = MatchId };

            ProfileMatchHistoryItem item = new ProfileMatchHistoryItem(result);

            Assert.Equal(string.Format(Strings.ProfileHistoryMatchFormat, MatchId), item.MatchText);
        }
    }
}
