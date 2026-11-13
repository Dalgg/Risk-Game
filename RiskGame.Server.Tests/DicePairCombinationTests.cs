using System.Collections.Generic;
using RiskGame.Server.Services;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class DicePairCombinationTests
    {
        private const int FirstPairIndex = 1;
        private const int SecondPairIndex = 2;
        private const int ExpectedFirstSum = 5;
        private const int ExpectedSecondSum = 9;

        [Fact]
        public void Constructor_ValidPairs_ComputesFirstSum()
        {
            List<int> firstPair = new List<int> { 2, 3 };
            List<int> secondPair = new List<int> { 4, 5 };

            DicePairCombination combination = new DicePairCombination(FirstPairIndex, firstPair, secondPair);

            Assert.Equal(ExpectedFirstSum, combination.FirstSum);
        }

        [Fact]
        public void Constructor_ValidPairs_ComputesSecondSum()
        {
            List<int> firstPair = new List<int> { 2, 3 };
            List<int> secondPair = new List<int> { 4, 5 };

            DicePairCombination combination = new DicePairCombination(FirstPairIndex, firstPair, secondPair);

            Assert.Equal(ExpectedSecondSum, combination.SecondSum);
        }

        [Fact]
        public void Constructor_ValidPairs_StoresPairIndex()
        {
            List<int> firstPair = new List<int> { 1, 1 };
            List<int> secondPair = new List<int> { 6, 6 };

            DicePairCombination combination = new DicePairCombination(SecondPairIndex, firstPair, secondPair);

            Assert.Equal(SecondPairIndex, combination.PairIndex);
        }
    }
}
