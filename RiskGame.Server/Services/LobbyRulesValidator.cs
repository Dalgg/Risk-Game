using System;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;

namespace RiskGame.Server.Services
{
    public static class LobbyRulesValidator
    {
        private const int ShortTurnSeconds = 30;
        private const int DefaultTurnSeconds = 60;
        private const int LongTurnSeconds = 90;

        public static void Validate(LobbyRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentException("Lobby rules are required.");
            }
            if (rules.RulesVariant != RulesVariant.Standard && rules.RulesVariant != RulesVariant.Fast)
            {
                throw new ArgumentException("The selected rules variant is not supported.");
            }
            bool isShortOrDefault = rules.TurnDurationSeconds == ShortTurnSeconds
                || rules.TurnDurationSeconds == DefaultTurnSeconds;
            if (!isShortOrDefault && rules.TurnDurationSeconds != LongTurnSeconds)
            {
                throw new ArgumentException("Turn duration must be 30, 60 or 90 seconds.");
            }
        }
    }
}
