using System;
using System.Linq;
using Grpc.Core;
using RiskGame.Contracts.Profile;
using RiskGame.Server.Services;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class ProfileHistoryTests
    {
        private const int FirstEntry = 0;
        private const int ZeroColumns = 0;
        private const string UpdatedNickname = "History Player";
        private const string AbandonmentReason = "Abandonment";

        [Fact]
        public void GetProfile_MoreThanFiveResults_ReturnsFive()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.SeedRecentMatches();

            UserProfile profile = setup.ReadProfile();

            Assert.Equal(ProfileHistoryBuilder.RecentLimit, profile.RecentMatches.Count);
        }

        [Fact]
        public void GetProfile_RecentResults_OrdersNewestFirst()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.SeedRecentMatches();

            UserProfile profile = setup.ReadProfile();

            Assert.Equal(ProfileHistoryBuilder.MostRecentMatchId, profile.RecentMatches[FirstEntry].MatchId);
        }

        [Fact]
        public void GetProfile_LaterForeignResult_DoesNotLeakOtherUserHistory()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.SeedRecentMatches();

            UserProfile profile = setup.ReadProfile();

            Assert.DoesNotContain(profile.RecentMatches,
                entry => entry.MatchId == ProfileHistoryBuilder.ForeignMatchId);
        }

        [Fact]
        public void GetProfile_UnfinishedMatch_ExcludesResult()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.SeedRecentMatches();

            UserProfile profile = setup.ReadProfile();

            Assert.DoesNotContain(profile.RecentMatches,
                entry => entry.MatchId == ProfileHistoryBuilder.UnfinishedMatchId);
        }

        [Fact]
        public void GetProfile_NoResults_ReturnsEmptyHistory()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();

            UserProfile profile = setup.ReadProfile();

            Assert.Empty(profile.RecentMatches);
        }

        [Fact]
        public void GetProfile_CancelledMatchWithoutResult_DoesNotInventHistory()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.SeedCancelledMatchWithoutResults();

            UserProfile profile = setup.ReadProfile();

            Assert.Empty(profile.RecentMatches);
        }

        [Fact]
        public void GetProfile_IdentifiersOutOfDateOrder_SortsByEndDate()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.SeedOutOfOrderIdentifiers();

            UserProfile profile = setup.ReadProfile();

            Assert.Equal(ProfileHistoryBuilder.NewerLowIdentifier, profile.RecentMatches[FirstEntry].MatchId);
        }

        [Fact]
        public void GetProfile_EqualEndDates_UsesDescendingMatchIdentifier()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.SeedTiedEndDates();

            UserProfile profile = setup.ReadProfile();

            Assert.Equal(ProfileHistoryBuilder.TieSecondId, profile.RecentMatches[FirstEntry].MatchId);
        }

        [Theory]
        [InlineData(ProfileHistoryBuilder.WinningPosition, true)]
        [InlineData(ProfileHistoryBuilder.LosingPosition, false)]
        public void GetProfile_FinalPosition_ReportsWinOrLoss(int position, bool isWinner)
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.AddResult(new ProfileHistoryBuilder.HistorySeed { FinalPosition = position });

            UserProfile profile = setup.ReadProfile();

            Assert.Equal(isWinner, profile.RecentMatches.Single().IsWinner);
        }

        [Fact]
        public void GetProfile_CompletedResult_ConvertsUtcDate()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.AddResult(new ProfileHistoryBuilder.HistorySeed());

            UserProfile profile = setup.ReadProfile();

            Assert.Equal(new DateTimeOffset(ProfileHistoryBuilder.BaseTime).ToUnixTimeSeconds(),
                profile.RecentMatches.Single().EndTimeUnixSeconds);
        }

        [Fact]
        public void GetProfile_ClaimedColumns_ReturnsStoredScore()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.AddResult(new ProfileHistoryBuilder.HistorySeed
            {
                FinalScore = ProfileHistoryBuilder.ClaimedColumns
            });

            UserProfile profile = setup.ReadProfile();

            Assert.Equal(ProfileHistoryBuilder.ClaimedColumns, profile.RecentMatches.Single().ClaimedColumns);
        }

        [Fact]
        public void GetProfile_UnknownScore_DistinguishesUnavailableValue()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.AddResult(new ProfileHistoryBuilder.HistorySeed());

            UserProfile profile = setup.ReadProfile();

            Assert.False(profile.RecentMatches.Single().HasClaimedColumns);
        }

        [Fact]
        public void GetProfile_KnownZeroScore_DistinguishesZeroFromUnknown()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.AddResult(new ProfileHistoryBuilder.HistorySeed { FinalScore = ZeroColumns });

            UserProfile profile = setup.ReadProfile();

            Assert.True(profile.RecentMatches.Single().HasClaimedColumns);
        }

        [Fact]
        public void GetProfile_UnknownPosition_DoesNotInventDefeat()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.AddResult(new ProfileHistoryBuilder.HistorySeed());

            UserProfile profile = setup.ReadProfile();

            Assert.False(profile.RecentMatches.Single().HasResult);
        }

        [Fact]
        public void GetProfile_Abandonment_PreservesEndReason()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.AddResult(new ProfileHistoryBuilder.HistorySeed { EndReason = AbandonmentReason });

            UserProfile profile = setup.ReadProfile();

            Assert.Equal(AbandonmentReason, profile.RecentMatches.Single().EndReason);
        }

        [Fact]
        public void GetProfile_MissingEndReason_ReturnsEmptyReason()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.AddResult(new ProfileHistoryBuilder.HistorySeed());

            UserProfile profile = setup.ReadProfile();

            Assert.Equal(string.Empty, profile.RecentMatches.Single().EndReason);
        }

        [Fact]
        public void UpdateNickname_ReturnsRecentHistory()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.SeedRecentMatches();

            UserProfile profile = setup.Repository.UpdateNickname(ProfileHistoryBuilder.UserId, UpdatedNickname);

            Assert.Equal(ProfileHistoryBuilder.RecentLimit, profile.RecentMatches.Count);
        }

        [Fact]
        public void UpdateAvatar_ReturnsRecentHistory()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.SeedRecentMatches();

            UserProfile profile = setup.Repository.UpdateAvatar(ProfileHistoryBuilder.UserId,
                ProfileCustomizationRules.TowerAvatarReference);

            Assert.Equal(ProfileHistoryBuilder.RecentLimit, profile.RecentMatches.Count);
        }

        [Fact]
        public void GetProfile_AuthenticatedUser_ReturnsOwnHistory()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.SeedRecentMatches();

            UserProfile profile = setup.ReadAuthenticatedProfile(ProfileHistoryBuilder.UserId);

            Assert.Equal(ProfileHistoryBuilder.MostRecentMatchId, profile.RecentMatches[FirstEntry].MatchId);
        }

        [Fact]
        public void GetProfile_AnotherUserIdentifier_Rejects()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();

            RpcException exception = (RpcException)Record.Exception(() =>
                setup.ReadAuthenticatedProfile(ProfileHistoryBuilder.OtherUserId))!;

            Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        }

        [Fact]
        public void GetProfile_ExpiredSession_Rejects()
        {
            using ProfileHistoryBuilder setup = new ProfileHistoryBuilder();
            setup.ExpireSession();

            RpcException exception = (RpcException)Record.Exception(() =>
                setup.ReadAuthenticatedProfile(ProfileHistoryBuilder.UserId))!;

            Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
        }
    }
}
