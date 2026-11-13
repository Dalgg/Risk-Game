using System.Collections.Generic;
using System.Linq;

namespace RiskGame.Server.Services
{
    public sealed class DicePairCombination
    {
        public DicePairCombination(int pairIndex, IReadOnlyList<int> firstPair, IReadOnlyList<int> secondPair)
        {
            PairIndex = pairIndex;
            FirstPairDice = new List<int>(firstPair).AsReadOnly();
            SecondPairDice = new List<int>(secondPair).AsReadOnly();
            FirstSum = FirstPairDice.Sum();
            SecondSum = SecondPairDice.Sum();
        }

        public int PairIndex { get; }

        public IReadOnlyList<int> FirstPairDice { get; }

        public IReadOnlyList<int> SecondPairDice { get; }

        public int FirstSum { get; }

        public int SecondSum { get; }
    }
}
