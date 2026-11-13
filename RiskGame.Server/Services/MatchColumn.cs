using System.Collections.Generic;
using System.Collections.ObjectModel;

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
    }
}
