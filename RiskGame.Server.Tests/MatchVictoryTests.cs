using System.Linq;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Server.Services;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class MatchVictoryTests
    {
        private const int MatchId = 201;
        private const int FirstUserId = 15;
        private const int SecondUserId = 20;
        private const int GuestUserId = -15;
        private const int ShortColumn = 2;
        private const int ShortHeight = 3;
        private const int OpenColumn = 4;
        private const int FirstClaimedColumn = 3;
        private const int SecondClaimedColumn = 12;
        private const int NoClaims = 0;
        private const int OneClaim = 1;
        private const int TwoClaims = 2;
        private const int OneStep = 1;
        private const int FirstPairIndex = 1;
        private static readonly int[] ShortDice = { 1, 1, 1, 1 };
        private static readonly int[] MixedDice = { 1, 1, 2, 2 };

        [Fact]
        public void ChoosePair_Summit_DoesNotClaimYet()
        {
            ActiveMatch match = CreateMatchAtSummit(NoClaims);

            MatchColumn column = GetColumn(match, ShortColumn);

            Assert.False(column.IsClaimed);
        }

        [Fact]
        public void StopTurn_Summit_ClaimsColumn()
        {
            ActiveMatch match = CreateMatchAtSummit(NoClaims);

            match.StopTurn(match.TurnId);

            Assert.True(GetColumn(match, ShortColumn).IsClaimed);
        }

        [Fact]
        public void StopTurn_Summit_AssignsOwner()
        {
            ActiveMatch match = CreateMatchAtSummit(NoClaims);

            match.StopTurn(match.TurnId);

            Assert.Equal(FirstUserId, GetColumn(match, ShortColumn).OwnerUserId);
        }

        [Fact]
        public void StopTurn_OnlyTwoClaims_DoesNotFinish()
        {
            ActiveMatch match = CreateMatchAtSummit(OneClaim);

            match.StopTurn(match.TurnId);

            Assert.False(match.IsFinished);
        }

        [Fact]
        public void StopTurn_ThirdClaim_FinishesMatch()
        {
            ActiveMatch match = CreateMatchAtSummit(TwoClaims);

            match.StopTurn(match.TurnId);

            Assert.True(match.IsFinished);
        }

        [Fact]
        public void StopTurn_ThirdClaim_AssignsWinner()
        {
            ActiveMatch match = CreateMatchAtSummit(TwoClaims);

            match.StopTurn(match.TurnId);

            Assert.Equal(FirstUserId, match.WinnerUserId);
        }

        [Fact]
        public void StopTurn_ThirdClaim_KeepsWinnerTurn()
        {
            ActiveMatch match = CreateMatchAtSummit(TwoClaims);

            match.StopTurn(match.TurnId);

            Assert.Equal(FirstUserId, match.CurrentTurnUserId);
        }

        [Fact]
        public void StopTurn_ThirdClaim_ClearsTemporary()
        {
            ActiveMatch match = CreateMatchAtSummit(TwoClaims);

            match.StopTurn(match.TurnId);

            Assert.Empty(match.TurnProgress.StepsByColumn);
        }

        [Fact]
        public void StopTurn_ThirdClaim_DisablesStop()
        {
            ActiveMatch match = CreateMatchAtSummit(TwoClaims);

            match.StopTurn(match.TurnId);

            Assert.False(match.CanStop);
        }

        [Fact]
        public void FinishedMatch_RejectsRoll()
        {
            ActiveMatch match = CreateMatchAtSummit(TwoClaims);
            match.StopTurn(match.TurnId);

            System.Exception? exception = Record.Exception(() => match.ApplyRoll(ShortDice, match.RollId));

            Assert.IsType<MatchActionException>(exception);
        }

        [Fact]
        public void FinishedMatch_RejectsChoose()
        {
            ActiveMatch match = CreateMatchAtSummit(TwoClaims);
            match.StopTurn(match.TurnId);

            System.Exception? exception = Record.Exception(() => match.ChoosePair(match.RollId, FirstPairIndex));

            Assert.IsType<MatchActionException>(exception);
        }

        [Fact]
        public void FinishedMatch_RejectsStop()
        {
            ActiveMatch match = CreateMatchAtSummit(TwoClaims);
            match.StopTurn(match.TurnId);

            System.Exception? exception = Record.Exception(() => match.StopTurn(match.TurnId));

            Assert.IsType<MatchActionException>(exception);
        }

        [Fact]
        public void ApplyRoll_BustAtSummit_DoesNotClaim()
        {
            ActiveMatch match = CreateMatchAtSummit(NoClaims);

            match.ApplyRoll(ShortDice, match.RollId);

            Assert.False(GetColumn(match, ShortColumn).IsClaimed);
        }

        [Fact]
        public void ClaimedColumn_RejectsOtherPlayer()
        {
            ActiveMatch match = CreateMatchAtSummit(NoClaims);
            match.StopTurn(match.TurnId);

            System.Exception? exception = Record.Exception(() => GetColumn(match, ShortColumn)
                .SaveProgress(SecondUserId, ShortHeight));

            Assert.IsType<MatchActionException>(exception);
        }

        [Fact]
        public void ClaimedColumn_RejectsOwner()
        {
            ActiveMatch match = CreateMatchAtSummit(NoClaims);
            match.StopTurn(match.TurnId);

            System.Exception? exception = Record.Exception(() => GetColumn(match, ShortColumn)
                .SaveProgress(FirstUserId, ShortHeight));

            Assert.IsType<MatchActionException>(exception);
        }

        [Fact]
        public void ApplyRoll_OnlyClaimedSums_BustsForOtherPlayer()
        {
            ActiveMatch match = CreateMatchAtSummit(NoClaims);
            match.StopTurn(match.TurnId);

            match.ApplyRoll(ShortDice, match.RollId);

            Assert.True(match.LastTurnBusted);
        }

        [Fact]
        public void ChoosePair_OneClaimedSum_AdvancesOnlyOpenColumn()
        {
            ActiveMatch match = CreateMatchAtSummit(NoClaims);
            match.StopTurn(match.TurnId);
            match.ApplyRoll(MixedDice, match.RollId);

            match.ChoosePair(match.RollId, FirstPairIndex);

            Assert.Equal(OpenColumn, match.TurnProgress.StepsByColumn.Keys.Single());
        }

        [Fact]
        public void ClaimedColumn_RemovesOtherPlayerProgress()
        {
            ActiveMatch match = CreateMatchAtSummit(NoClaims);
            GetColumn(match, ShortColumn).SaveProgress(SecondUserId, OneStep);

            match.StopTurn(match.TurnId);

            Assert.Equal(NoClaims, GetColumn(match, ShortColumn).StepsByUserId[SecondUserId]);
        }

        [Fact]
        public void Guest_CanWinWithNegativeIdentifier()
        {
            ActiveMatch match = CreateMatchAtSummit(TwoClaims, GuestUserId);

            match.StopTurn(match.TurnId);

            Assert.Equal(GuestUserId, match.WinnerUserId);
        }

        private static ActiveMatch CreateMatchAtSummit(int previousClaims, int firstUserId = FirstUserId)
        {
            LobbyDetails lobby = new LobbyDetails { Rules = new LobbyRules() };
            lobby.Players.Add(new LobbyPlayer { Player = new PlayerSummary { UserId = firstUserId } });
            lobby.Players.Add(new LobbyPlayer { Player = new PlayerSummary { UserId = SecondUserId } });
            ActiveMatch match = new ActiveMatch(MatchId, lobby);
            if (previousClaims >= OneClaim)
            {
                MatchColumn column = GetColumn(match, FirstClaimedColumn);
                column.SaveProgress(firstUserId, column.MaximumSteps);
            }
            if (previousClaims >= TwoClaims)
            {
                MatchColumn column = GetColumn(match, SecondClaimedColumn);
                column.SaveProgress(firstUserId, column.MaximumSteps);
            }
            match.ApplyRoll(ShortDice, match.RollId);
            match.ChoosePair(match.RollId, FirstPairIndex);
            match.ApplyRoll(ShortDice, match.RollId);
            match.ChoosePair(match.RollId, FirstPairIndex);
            return match;
        }

        private static MatchColumn GetColumn(ActiveMatch match, int columnNumber)
        {
            return match.Columns.Single(column => column.ColumnNumber == columnNumber);
        }
    }
}

