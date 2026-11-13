using System.Collections.Generic;
using RiskGame.Exceptions;
using RiskGame.Server.Services;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class DicePairGeneratorTests
    {
        private const int ExpectedCombinationCount = 3;
        private const int FirstOptionListIndex = 0;
        private const int ThirdOptionListIndex = 2;
        private const int ExpectedFirstOptionFirstSum = 3;
        private const int ExpectedThirdOptionSecondSum = 6;

        [Fact]
        public void BuildCombinations_FourValidDice_ReturnsThreeOptions()
        {
            List<int> validDice = new List<int> { 1, 2, 4, 6 };

            IReadOnlyList<DicePairCombination> combinations = DicePairGenerator.BuildCombinations(validDice);

            Assert.Equal(ExpectedCombinationCount, combinations.Count);
        }

        [Fact]
        public void BuildCombinations_FirstOption_ComputesExpectedFirstSum()
        {
            List<int> validDice = new List<int> { 1, 2, 4, 6 };

            IReadOnlyList<DicePairCombination> combinations = DicePairGenerator.BuildCombinations(validDice);

            Assert.Equal(ExpectedFirstOptionFirstSum, combinations[FirstOptionListIndex].FirstSum);
        }

        [Fact]
        public void BuildCombinations_ThirdOption_ComputesExpectedSecondSum()
        {
            List<int> validDice = new List<int> { 1, 2, 4, 6 };

            IReadOnlyList<DicePairCombination> combinations = DicePairGenerator.BuildCombinations(validDice);

            Assert.Equal(ExpectedThirdOptionSecondSum, combinations[ThirdOptionListIndex].SecondSum);
        }

        [Fact]
        public void BuildCombinations_RepeatedDiceFaces_ReturnsThreeOptions()
        {
            List<int> repeatedDice = new List<int> { 2, 2, 5, 5 };

            IReadOnlyList<DicePairCombination> combinations = DicePairGenerator.BuildCombinations(repeatedDice);

            Assert.Equal(ExpectedCombinationCount, combinations.Count);
        }

        [Fact]
        public void BuildCombinations_FewerThanFourDice_ThrowsMatchActionException()
        {
            List<int> shortDiceList = new List<int> { 1, 2, 3 };

            Assert.Throws<MatchActionException>(() => DicePairGenerator.BuildCombinations(shortDiceList));
        }

        [Fact]
        public void BuildCombinations_MoreThanFourDice_ThrowsMatchActionException()
        {
            List<int> longDiceList = new List<int> { 1, 2, 3, 4, 5 };

            Assert.Throws<MatchActionException>(() => DicePairGenerator.BuildCombinations(longDiceList));
        }

        [Fact]
        public void BuildCombinations_DieBelowMinimum_ThrowsMatchActionException()
        {
            List<int> lowInvalidDice = new List<int> { 0, 2, 4, 6 };

            Assert.Throws<MatchActionException>(() => DicePairGenerator.BuildCombinations(lowInvalidDice));
        }

        [Fact]
        public void BuildCombinations_DieAboveMaximum_ThrowsMatchActionException()
        {
            List<int> highInvalidDice = new List<int> { 1, 2, 4, 7 };

            Assert.Throws<MatchActionException>(() => DicePairGenerator.BuildCombinations(highInvalidDice));
        }
    }
}
