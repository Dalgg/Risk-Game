using System.Linq;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Exceptions;
using RiskGame.Server.Services;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class ActiveMatchProgressTests
    {
        private const int MatchIdentifier = 101;
        private const int FirstUserId = 15;
        private const int SecondUserId = 20;
        private const int FirstPairIndex = 1;
        private const int ThirdPairIndex = 3;
        private const int NoPreferredColumn = 0;
        private const int InitialStep = 0;
        private const int FirstStep = 1;
        private const int SecondStep = 2;
        private const int LastShortColumnStep = 3;
        private const int FirstColumn = 5;
        private const int SecondColumn = 9;
        private const int ThirdColumn = 7;
        private const int UnavailableColumn = 6;
        private const int ShortColumn = 2;
        private const int OtherShortColumn = 3;
        private const int TwoMarkers = 2;
        private const int ThreeMarkers = 3;
        private const long InitialRollId = 0;
        private const long FirstRollId = 1;
        private const long SecondRollId = 2;
        private const long ThirdRollId = 3;
        private static readonly int[] DistinctSumsDice = { 2, 3, 4, 5 };
        private static readonly int[] CentralDoubleDice = { 3, 4, 3, 4 };
        private static readonly int[] ShortDoubleDice = { 1, 1, 1, 1 };
        private static readonly int[] TwoNewColumnsDice = { 1, 1, 1, 2 };
        private static readonly int[] OneAvailableSumDice = { 1, 4, 3, 3 };
        private static readonly int[] NoAvailableSumDice = { 1, 1, 6, 6 };

        [Fact]
        public void ChoosePair_DistinctSums_AdvancesFirstColumn()
        {
            ActiveMatch match = CreateMatch();
            match.ApplyRoll(DistinctSumsDice, InitialRollId);

            match.ChoosePair(FirstRollId, FirstPairIndex);

            Assert.Equal(FirstStep, match.TurnProgress.StepsByColumn[FirstColumn]);
        }

        [Fact]
        public void ChoosePair_DistinctSums_AdvancesSecondColumn()
        {
            ActiveMatch match = CreateMatch();
            match.ApplyRoll(DistinctSumsDice, InitialRollId);

            match.ChoosePair(FirstRollId, FirstPairIndex);

            Assert.Equal(FirstStep, match.TurnProgress.StepsByColumn[SecondColumn]);
        }

        [Fact]
        public void ChoosePair_DistinctSums_PreservesPermanentProgress()
        {
            ActiveMatch match = CreateMatch();
            match.ApplyRoll(DistinctSumsDice, InitialRollId);

            match.ChoosePair(FirstRollId, FirstPairIndex);

            Assert.True(match.Columns.All(column =>
                column.StepsByUserId.Values.All(progress => progress == InitialStep)));
        }

        [Fact]
        public void ChoosePair_DoubleSum_UsesOneMarker()
        {
            ActiveMatch match = CreateMatch();
            match.ApplyRoll(DistinctSumsDice, InitialRollId);

            match.ChoosePair(FirstRollId, ThirdPairIndex);

            Assert.Single(match.TurnProgress.StepsByColumn);
        }

        [Fact]
        public void ChoosePair_DoubleSum_AdvancesTwice()
        {
            ActiveMatch match = CreateMatch();
            match.ApplyRoll(DistinctSumsDice, InitialRollId);

            match.ChoosePair(FirstRollId, ThirdPairIndex);

            Assert.Equal(SecondStep, match.TurnProgress.StepsByColumn[ThirdColumn]);
        }

        [Fact]
        public void ChoosePair_RepeatedRoll_KeepsMarkerCount()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(DistinctSumsDice, FirstRollId);

            match.ChoosePair(SecondRollId, FirstPairIndex);

            Assert.Equal(TwoMarkers, match.TurnProgress.StepsByColumn.Count);
        }

        [Fact]
        public void ChoosePair_RepeatedRoll_AdvancesFirstColumn()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(DistinctSumsDice, FirstRollId);

            match.ChoosePair(SecondRollId, FirstPairIndex);

            Assert.Equal(SecondStep, match.TurnProgress.StepsByColumn[FirstColumn]);
        }

        [Fact]
        public void ChoosePair_RepeatedRoll_AdvancesSecondColumn()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(DistinctSumsDice, FirstRollId);

            match.ChoosePair(SecondRollId, FirstPairIndex);

            Assert.Equal(SecondStep, match.TurnProgress.StepsByColumn[SecondColumn]);
        }

        [Theory]
        [InlineData(ShortColumn)]
        [InlineData(OtherShortColumn)]
        public void ChoosePair_OneFreeMarker_UsesThirdMarker(int preferredColumn)
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(TwoNewColumnsDice, FirstRollId);

            match.ChoosePair(SecondRollId, FirstPairIndex, preferredColumn);

            Assert.Equal(ThreeMarkers, match.TurnProgress.StepsByColumn.Count);
        }

        [Theory]
        [InlineData(ShortColumn)]
        [InlineData(OtherShortColumn)]
        public void ChoosePair_OneFreeMarker_AdvancesRequestedColumn(int preferredColumn)
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(TwoNewColumnsDice, FirstRollId);

            match.ChoosePair(SecondRollId, FirstPairIndex, preferredColumn);

            Assert.Equal(FirstStep, match.TurnProgress.StepsByColumn[preferredColumn]);
        }

        [Fact]
        public void ChoosePair_MissingColumnChoice_ThrowsMatchActionException()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(TwoNewColumnsDice, FirstRollId);

            System.Exception? rejectedException = Record.Exception(() => match.ChoosePair(SecondRollId, FirstPairIndex));

            Assert.IsType<MatchActionException>(rejectedException);
        }

        [Fact]
        public void ChoosePair_MissingColumnChoice_PreservesRevision()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(TwoNewColumnsDice, FirstRollId);
            long originalRevision = match.Revision;

            Record.Exception(() => match.ChoosePair(SecondRollId, FirstPairIndex));

            Assert.Equal(originalRevision, match.Revision);
        }

        [Fact]
        public void ChoosePair_MissingColumnChoice_PreservesMarkerCount()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(TwoNewColumnsDice, FirstRollId);

            Record.Exception(() => match.ChoosePair(SecondRollId, FirstPairIndex));

            Assert.Equal(TwoMarkers, match.TurnProgress.StepsByColumn.Count);
        }

        [Fact]
        public void ChoosePair_MissingColumnChoice_KeepsChoicePending()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(TwoNewColumnsDice, FirstRollId);

            Record.Exception(() => match.ChoosePair(SecondRollId, FirstPairIndex));

            Assert.True(match.AwaitingPairChoice);
        }

        [Fact]
        public void ChoosePair_MissingColumnChoice_KeepsSelectionEmpty()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(TwoNewColumnsDice, FirstRollId);

            Record.Exception(() => match.ChoosePair(SecondRollId, FirstPairIndex));

            Assert.Equal(NoPreferredColumn, match.SelectedPairIndex);
        }

        [Fact]
        public void ChoosePair_UnrelatedColumn_ThrowsMatchActionException()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(TwoNewColumnsDice, FirstRollId);

            System.Exception? rejectedException = Record.Exception(() => match.ChoosePair(SecondRollId, FirstPairIndex, UnavailableColumn));

            Assert.IsType<MatchActionException>(rejectedException);
        }

        [Fact]
        public void ChoosePair_UnrelatedColumn_DoesNotOpenColumn()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(TwoNewColumnsDice, FirstRollId);

            Record.Exception(() => match.ChoosePair(SecondRollId, FirstPairIndex, UnavailableColumn));

            Assert.False(match.TurnProgress.StepsByColumn.ContainsKey(UnavailableColumn));
        }

        [Fact]
        public void ChoosePair_UnrelatedColumn_PreservesMarkerCount()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(TwoNewColumnsDice, FirstRollId);

            Record.Exception(() => match.ChoosePair(SecondRollId, FirstPairIndex, UnavailableColumn));

            Assert.Equal(TwoMarkers, match.TurnProgress.StepsByColumn.Count);
        }

        [Fact]
        public void ChoosePair_UnnecessaryColumn_ThrowsMatchActionException()
        {
            ActiveMatch match = CreateMatch();
            match.ApplyRoll(DistinctSumsDice, InitialRollId);

            System.Exception? rejectedException = Record.Exception(() => match.ChoosePair(FirstRollId, FirstPairIndex, FirstColumn));

            Assert.IsType<MatchActionException>(rejectedException);
        }

        [Fact]
        public void ChoosePair_UnnecessaryColumn_PreservesEmptyProgress()
        {
            ActiveMatch match = CreateMatch();
            match.ApplyRoll(DistinctSumsDice, InitialRollId);

            Record.Exception(() => match.ChoosePair(FirstRollId, FirstPairIndex, FirstColumn));

            Assert.Empty(match.TurnProgress.StepsByColumn);
        }

        [Fact]
        public void ChoosePair_ThreeMarkers_KeepsThreeMarkers()
        {
            ActiveMatch match = CreateMatchWithThreeMarkers();
            match.ApplyRoll(OneAvailableSumDice, SecondRollId);

            match.ChoosePair(ThirdRollId, FirstPairIndex);

            Assert.Equal(ThreeMarkers, match.TurnProgress.StepsByColumn.Count);
        }

        [Fact]
        public void ChoosePair_ThreeMarkers_AdvancesExistingColumn()
        {
            ActiveMatch match = CreateMatchWithThreeMarkers();
            match.ApplyRoll(OneAvailableSumDice, SecondRollId);

            match.ChoosePair(ThirdRollId, FirstPairIndex);

            Assert.Equal(SecondStep, match.TurnProgress.StepsByColumn[FirstColumn]);
        }

        [Fact]
        public void ChoosePair_ThreeMarkers_DoesNotOpenFourthColumn()
        {
            ActiveMatch match = CreateMatchWithThreeMarkers();
            match.ApplyRoll(OneAvailableSumDice, SecondRollId);

            match.ChoosePair(ThirdRollId, FirstPairIndex);

            Assert.False(match.TurnProgress.StepsByColumn.ContainsKey(UnavailableColumn));
        }

        [Fact]
        public void ChoosePair_NoAvailableSum_ThrowsMatchActionException()
        {
            ActiveMatch match = CreateMatchWithThreeMarkers();
            match.ApplyRoll(NoAvailableSumDice, SecondRollId);

            System.Exception? rejectedException = Record.Exception(() => match.ChoosePair(ThirdRollId, FirstPairIndex));

            Assert.IsType<MatchActionException>(rejectedException);
        }

        [Fact]
        public void ChoosePair_NoAvailableSum_PreservesRevision()
        {
            ActiveMatch match = CreateMatchWithThreeMarkers();
            match.ApplyRoll(NoAvailableSumDice, SecondRollId);
            long originalRevision = match.Revision;

            Record.Exception(() => match.ChoosePair(ThirdRollId, FirstPairIndex));

            Assert.Equal(originalRevision, match.Revision);
        }

        [Fact]
        public void ChoosePair_NoAvailableSum_PreservesMarkerCount()
        {
            ActiveMatch match = CreateMatchWithThreeMarkers();
            match.ApplyRoll(NoAvailableSumDice, SecondRollId);

            Record.Exception(() => match.ChoosePair(ThirdRollId, FirstPairIndex));

            Assert.Equal(ThreeMarkers, match.TurnProgress.StepsByColumn.Count);
        }

        [Fact]
        public void ChoosePair_NoAvailableSum_KeepsChoicePending()
        {
            ActiveMatch match = CreateMatchWithThreeMarkers();
            match.ApplyRoll(NoAvailableSumDice, SecondRollId);

            Record.Exception(() => match.ChoosePair(ThirdRollId, FirstPairIndex));

            Assert.True(match.AwaitingPairChoice);
        }

        [Fact]
        public void ChoosePair_DoubleNearSummit_StopsAtMaximumHeight()
        {
            ActiveMatch match = CreateMatch();
            match.ApplyRoll(ShortDoubleDice, InitialRollId);
            match.ChoosePair(FirstRollId, FirstPairIndex);
            match.ApplyRoll(ShortDoubleDice, FirstRollId);
            match.ChoosePair(SecondRollId, FirstPairIndex);

            int temporaryStep = match.TurnProgress.StepsByColumn[ShortColumn];

            Assert.Equal(LastShortColumnStep, temporaryStep);
        }

        [Fact]
        public void ApplyRoll_CompletedColumn_ReportsUnplayableOption()
        {
            ActiveMatch match = CreateMatch();
            match.ApplyRoll(ShortDoubleDice, InitialRollId);
            match.ChoosePair(FirstRollId, FirstPairIndex);
            match.ApplyRoll(ShortDoubleDice, FirstRollId);
            match.ChoosePair(SecondRollId, FirstPairIndex);

            match.ApplyRoll(ShortDoubleDice, SecondRollId);

            Assert.False(match.TurnProgress.IsPlayable(match.PairOptions[InitialStep]));
        }

        [Fact]
        public void ChoosePair_CompletedColumn_ThrowsMatchActionException()
        {
            ActiveMatch match = CreateMatch();
            match.ApplyRoll(ShortDoubleDice, InitialRollId);
            match.ChoosePair(FirstRollId, FirstPairIndex);
            match.ApplyRoll(ShortDoubleDice, FirstRollId);
            match.ChoosePair(SecondRollId, FirstPairIndex);
            match.ApplyRoll(ShortDoubleDice, SecondRollId);

            System.Exception? rejectedException = Record.Exception(() => match.ChoosePair(ThirdRollId, FirstPairIndex));

            Assert.IsType<MatchActionException>(rejectedException);
        }

        [Fact]
        public void ChoosePair_DuplicateChoice_ThrowsMatchActionException()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();

            System.Exception? rejectedException = Record.Exception(() => match.ChoosePair(FirstRollId, FirstPairIndex));

            Assert.IsType<MatchActionException>(rejectedException);
        }

        [Fact]
        public void ChoosePair_DuplicateChoice_PreservesRevision()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            long originalRevision = match.Revision;

            Record.Exception(() => match.ChoosePair(FirstRollId, FirstPairIndex));

            Assert.Equal(originalRevision, match.Revision);
        }

        [Fact]
        public void ChoosePair_DuplicateChoice_DoesNotAdvanceAgain()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();

            Record.Exception(() => match.ChoosePair(FirstRollId, FirstPairIndex));

            Assert.Equal(FirstStep, match.TurnProgress.StepsByColumn[FirstColumn]);
        }

        [Fact]
        public void ChoosePair_OldRollId_ThrowsMatchActionException()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(DistinctSumsDice, FirstRollId);

            System.Exception? rejectedException = Record.Exception(() => match.ChoosePair(FirstRollId, FirstPairIndex));

            Assert.IsType<MatchActionException>(rejectedException);
        }

        [Fact]
        public void ChoosePair_OldRollId_PreservesRevision()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(DistinctSumsDice, FirstRollId);
            long originalRevision = match.Revision;

            Record.Exception(() => match.ChoosePair(FirstRollId, FirstPairIndex));

            Assert.Equal(originalRevision, match.Revision);
        }

        [Fact]
        public void ChoosePair_OldRollId_DoesNotAdvanceAgain()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(DistinctSumsDice, FirstRollId);

            Record.Exception(() => match.ChoosePair(FirstRollId, FirstPairIndex));

            Assert.Equal(FirstStep, match.TurnProgress.StepsByColumn[FirstColumn]);
        }

        private static ActiveMatch CreateMatchWithThreeMarkers()
        {
            ActiveMatch match = CreateMatchWithTwoMarkers();
            match.ApplyRoll(CentralDoubleDice, FirstRollId);
            match.ChoosePair(SecondRollId, FirstPairIndex);
            return match;
        }

        private static ActiveMatch CreateMatchWithTwoMarkers()
        {
            ActiveMatch match = CreateMatch();
            match.ApplyRoll(DistinctSumsDice, InitialRollId);
            match.ChoosePair(FirstRollId, FirstPairIndex);
            return match;
        }

        private static ActiveMatch CreateMatch()
        {
            LobbyDetails lobby = new LobbyDetails { Rules = new LobbyRules() };
            lobby.Players.Add(new LobbyPlayer { Player = new PlayerSummary { UserId = FirstUserId } });
            lobby.Players.Add(new LobbyPlayer { Player = new PlayerSummary { UserId = SecondUserId } });
            return new ActiveMatch(MatchIdentifier, lobby);
        }
    }
}
