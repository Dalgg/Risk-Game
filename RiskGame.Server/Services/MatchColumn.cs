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

        public bool IsClaimed { get; private set; }

        public int OwnerUserId { get; private set; }

        public IReadOnlyDictionary<int, int> StepsByUserId { get; }

        public void SaveProgress(int userId, int step)
        {
            if (IsClaimed)
            {
                throw new MatchActionException("The column has already been claimed.");
            }
            if (!stepsByUserId.ContainsKey(userId) || step < InitialStep || step > MaximumSteps)
            {
                throw new MatchActionException("The permanent progress does not belong to a valid board position.");
            }
            if (step < stepsByUserId[userId])
            {
                throw new MatchActionException("Permanent progress cannot move backwards.");
            }
            stepsByUserId[userId] = step;
            if (step == MaximumSteps)
            {
                IsClaimed = true;
                OwnerUserId = userId;
                foreach (int participantId in new List<int>(stepsByUserId.Keys))
                {
                    if (participantId != userId)
                    {
                        stepsByUserId[participantId] = InitialStep;
                    }
                }
            }
        }
    }
}
