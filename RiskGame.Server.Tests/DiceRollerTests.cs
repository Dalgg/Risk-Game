using System.Collections.Generic;
using System.Linq;
using RiskGame.Server.Services;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class DiceRollerTests
    {
        private const int ExpectedDiceCount = 4;
        private const int MinimumDieFace = 1;
        private const int MaximumDieFace = 6;

        [Fact]
        public void RollFourDice_Called_ReturnsFourValues()
        {
            IReadOnlyList<int> rolledDice = DiceRoller.RollFourDice();

            Assert.Equal(ExpectedDiceCount, rolledDice.Count);
        }

        [Fact]
        public void RollFourDice_Called_AllValuesWithinStandardRange()
        {
            IReadOnlyList<int> rolledDice = DiceRoller.RollFourDice();

            Assert.True(rolledDice.All(dieValue => dieValue >= MinimumDieFace && dieValue <= MaximumDieFace));
        }
    }
}
