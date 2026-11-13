using System.Collections.Generic;
using RiskGame.Exceptions;

namespace RiskGame.Server.Services
{
    public static class DicePairGenerator
    {
        private const int DiceCount = 4;
        private const int MinimumFace = 1;
        private const int MaximumFace = 6;
        private const int FirstDieIndex = 0;
        private const int SecondDieIndex = 1;
        private const int ThirdDieIndex = 2;
        private const int FourthDieIndex = 3;
        private const int FirstPairIndex = 1;
        private const int SecondPairIndex = 2;
        private const int ThirdPairIndex = 3;

        public static IReadOnlyList<DicePairCombination> BuildCombinations(IReadOnlyList<int> dice)
        {
            ValidateDice(dice);
            List<DicePairCombination> combinations = new List<DicePairCombination>
            {
                new DicePairCombination(FirstPairIndex,
                    new List<int> { dice[FirstDieIndex], dice[SecondDieIndex] },
                    new List<int> { dice[ThirdDieIndex], dice[FourthDieIndex] }),
                new DicePairCombination(SecondPairIndex,
                    new List<int> { dice[FirstDieIndex], dice[ThirdDieIndex] },
                    new List<int> { dice[SecondDieIndex], dice[FourthDieIndex] }),
                new DicePairCombination(ThirdPairIndex,
                    new List<int> { dice[FirstDieIndex], dice[FourthDieIndex] },
                    new List<int> { dice[SecondDieIndex], dice[ThirdDieIndex] })
            };
            return combinations.AsReadOnly();
        }

        private static void ValidateDice(IReadOnlyList<int> dice)
        {
            if (dice.Count != DiceCount)
            {
                throw new MatchActionException("A roll requires exactly four dice.");
            }
            foreach (int faceValue in dice)
            {
                if (faceValue < MinimumFace || faceValue > MaximumFace)
                {
                    throw new MatchActionException("Each die must have a face value between one and six.");
                }
            }
        }
    }
}
