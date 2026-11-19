using System.Collections.Generic;
using System.Collections.ObjectModel;
using RiskGame.Exceptions;

namespace RiskGame.Server.Services
{
    public sealed class MatchColumn
    {
        private const int InitialStep = 0;
        private readonly Dictionary<int, int> stepsByUserId = new Dictionary<int, int>();

        public MatchColumn(int columnNumber, int maximumSteps, IReadOnlyList<MatchParticipant> participants)
        {
            ColumnNumber = columnNumber;
            MaximumSteps = maximumSteps;
            foreach (MatchParticipant participant in participants)
            {
                stepsByUserId.Add(participant.UserId, InitialStep);
            }
            StepsByUserId = new ReadOnlyDictionary<int, int>(stepsByUserId);
        }

        public int ColumnNumber { get; }

        public int MaximumSteps { get; }

        public IReadOnlyDictionary<int, int> StepsByUserId { get; }

        public void SaveProgress(int userId, int step)
        {
            if (!stepsByUserId.ContainsKey(userId) || step < InitialStep || step > MaximumSteps)
            {
                throw new MatchActionException("The permanent progress does not belong to a valid board position.");
            }
            if (step < stepsByUserId[userId])
            {
                throw new MatchActionException("Permanent progress cannot move backwards.");
            }
            stepsByUserId[userId] = step;
        }
    }
}
