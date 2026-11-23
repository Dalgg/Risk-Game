using System.Collections.Generic;
using System.Linq;
using RiskGame.Contracts.Match;
using RiskGame.Exceptions;
using RiskGame.Models;

namespace RiskGame.Server.Services
{
    public sealed class MatchResultBuilder
    {
        private const int FirstPlace = 1;
        private const int MinimumRegisteredIdentifier = 1;

        public IReadOnlyList<MatchResult> Build(ActiveMatch match)
        {
            if (!match.IsFinished)
            {
                throw new MatchActionException("Only completed matches have final results.");
            }

            List<MatchParticipant> orderedPlayers = new List<MatchParticipant>();
            if (match.EndReason != MatchEndReason.Cancelled)
            {
                orderedPlayers = match.Participants
                    .OrderByDescending(player => player.UserId == match.WinnerUserId)
                    .ThenByDescending(player => CountClaims(match, player.UserId))
                    .ThenByDescending(player => CountSteps(match, player.UserId))
                    .ThenBy(player => player.UserId)
                    .ToList();
            }
            List<MatchResult> results = new List<MatchResult>();
            int position = FirstPlace;
            foreach (MatchParticipant player in orderedPlayers)
            {
                bool isRegistered = !player.IsGuest && player.UserId >= MinimumRegisteredIdentifier;
                if (isRegistered)
                {
                    results.Add(new MatchResult
                    {
                        IdMatch = match.MatchId,
                        IdUser = player.UserId,
                        FinalPosition = position,
                        FinalScore = CountClaims(match, player.UserId)
                    });
                }
                position++;
            }
            return results.AsReadOnly();
        }

        private int CountClaims(ActiveMatch match, int userId)
        {
            return match.Columns.Count(column => column.IsClaimed && column.OwnerUserId == userId);
        }

        private int CountSteps(ActiveMatch match, int userId)
        {
            return match.Columns.Sum(column => column.StepsByUserId[userId]);
        }
    }
}
