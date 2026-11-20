using RiskGame.Contracts.Common;
using RiskGame.Server.Services;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class MatchVariantTests
    {
        private const int StandardGoal = 3;
        private const int FastGoal = 2;
        private const int WinnerId = 41;

        [Theory]
        [InlineData(RulesVariant.Standard, StandardGoal)]
        [InlineData(RulesVariant.Fast, FastGoal)]
        public void ColumnsToWin_SelectedVariant_UsesExpectedGoal(RulesVariant variant, int goal)
        {
            ActiveMatch match = new CompletedMatchBuilder().WithRules(variant).CreateReadyMatch();

            Assert.Equal(goal, match.ColumnsToWin);
        }

        [Fact]
        public void StopTurn_FastSecondColumn_FinishesMatch()
        {
            ActiveMatch match = new CompletedMatchBuilder().WithRules(RulesVariant.Fast)
                .WithOnePreviousClaim().CreateReadyMatch();

            match.StopTurn(match.TurnId);

            Assert.True(match.IsFinished);
        }

        [Fact]
        public void StopTurn_FastSecondColumn_AssignsWinner()
        {
            ActiveMatch match = new CompletedMatchBuilder().WithRules(RulesVariant.Fast)
                .WithOnePreviousClaim().CreateReadyMatch();

            match.StopTurn(match.TurnId);

            Assert.Equal(WinnerId, match.WinnerUserId);
        }

        [Fact]
        public void StopTurn_StandardSecondColumn_ContinuesMatch()
        {
            ActiveMatch match = new CompletedMatchBuilder().WithOnePreviousClaim().CreateReadyMatch();

            match.StopTurn(match.TurnId);

            Assert.False(match.IsFinished);
        }
    }
}
