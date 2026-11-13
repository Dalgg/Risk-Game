using System.Collections.Generic;
using System.Security.Cryptography;

namespace RiskGame.Server.Services
{
    public static class DiceRoller
    {
        private const int DiceCount = 4;
        private const int MinimumFace = 1;
        private const int ExclusiveMaximumFace = 7;

        public static IReadOnlyList<int> RollFourDice()
        {
            List<int> dice = new List<int>();
            while (dice.Count < DiceCount)
            {
                dice.Add(RandomNumberGenerator.GetInt32(MinimumFace, ExclusiveMaximumFace));
            }
            return dice.AsReadOnly();
        }
    }
}
