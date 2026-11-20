using System;
using System.Collections.Generic;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Server.Services;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class RecordingMatchRepository : ICompletedMatchRepository
    {
        private bool shouldFail;

        public int SaveAttempts { get; private set; }

        public int SavedMatches { get; private set; }

        public IReadOnlyList<MatchResult> Results { get; private set; } = Array.Empty<MatchResult>();

        public void FailNextSave()
        {
            shouldFail = true;
        }

        public void SaveCompletedMatch(ActiveMatch match)
        {
            SaveAttempts++;
            if (shouldFail)
            {
                shouldFail = false;
                throw new DataOperationException("The simulated database is temporarily unavailable.");
            }
            Results = new MatchResultBuilder().Build(match);
            SavedMatches++;
        }
    }
}
