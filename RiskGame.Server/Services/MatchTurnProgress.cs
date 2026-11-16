using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RiskGame.Exceptions;

namespace RiskGame.Server.Services
{
    public sealed class MatchTurnProgress
    {
        private const int MaximumMarkers = 3;
        private const int SingleStep = 1;
        private const int NoPreferredColumn = 0;
        private readonly int userId;
        private readonly IReadOnlyList<MatchColumn> columns;
        private readonly Dictionary<int, int> stepsByColumn = new Dictionary<int, int>();

        public MatchTurnProgress(int userId, IReadOnlyList<MatchColumn> columns)
        {
            this.userId = userId;
            this.columns = columns;
            StepsByColumn = new ReadOnlyDictionary<int, int>(stepsByColumn);
        }

        public IReadOnlyDictionary<int, int> StepsByColumn { get; }

        public int MaximumTemporaryMarkers => MaximumMarkers;

        public bool IsPlayable(DicePairCombination combination)
        {
            return CanAdvance(combination.FirstSum) || CanAdvance(combination.SecondSum);
        }

        public bool RequiresColumnChoice(DicePairCombination combination)
        {
            return combination.FirstSum != combination.SecondSum
                && stepsByColumn.Count == MaximumMarkers - SingleStep
                && !stepsByColumn.ContainsKey(combination.FirstSum)
                && !stepsByColumn.ContainsKey(combination.SecondSum)
                && CanAdvance(combination.FirstSum) && CanAdvance(combination.SecondSum);
        }

        public void ApplyCombination(DicePairCombination combination, int preferredColumn)
        {
            ValidateColumnChoice(combination, preferredColumn);
            Dictionary<int, int> proposedSteps = new Dictionary<int, int>(stepsByColumn);
            List<int> columnsToAdvance = new List<int> { combination.FirstSum, combination.SecondSum };
            if (preferredColumn != NoPreferredColumn)
            {
                columnsToAdvance = new List<int> { preferredColumn };
            }
            bool hasAdvanced = false;
            foreach (int columnNumber in columnsToAdvance)
            {
                if (AdvanceColumn(proposedSteps, columnNumber))
                {
                    hasAdvanced = true;
                }
            }
            if (!hasAdvanced)
            {
                throw new MatchActionException("The selected grouping cannot advance any temporary marker.");
            }
            stepsByColumn.Clear();
            foreach (KeyValuePair<int, int> progress in proposedSteps)
            {
                stepsByColumn.Add(progress.Key, progress.Value);
            }
        }

        private bool CanAdvance(int columnNumber)
        {
            MatchColumn column = GetColumn(columnNumber);
            int currentStep = column.StepsByUserId[userId];
            if (stepsByColumn.ContainsKey(columnNumber))
            {
                currentStep = stepsByColumn[columnNumber];
            }
            return currentStep < column.MaximumSteps
                && (stepsByColumn.ContainsKey(columnNumber) || stepsByColumn.Count < MaximumMarkers);
        }

        private void ValidateColumnChoice(DicePairCombination combination, int preferredColumn)
        {
            if (RequiresColumnChoice(combination))
            {
                if (preferredColumn != combination.FirstSum && preferredColumn != combination.SecondSum)
                {
                    throw new MatchActionException("Choose which new column receives the last temporary marker.");
                }
            }
            else if (preferredColumn != NoPreferredColumn)
            {
                throw new MatchActionException("This grouping does not allow skipping an available advance.");
            }
        }

        private bool AdvanceColumn(Dictionary<int, int> proposedSteps, int columnNumber)
        {
            MatchColumn column = GetColumn(columnNumber);
            int currentStep = column.StepsByUserId[userId];
            if (proposedSteps.ContainsKey(columnNumber))
            {
                currentStep = proposedSteps[columnNumber];
            }
            if (currentStep >= column.MaximumSteps
                || (!proposedSteps.ContainsKey(columnNumber) && proposedSteps.Count >= MaximumMarkers))
            {
                return false;
            }
            proposedSteps[columnNumber] = currentStep + SingleStep;
            return true;
        }

        private MatchColumn GetColumn(int columnNumber)
        {
            MatchColumn? column = columns.FirstOrDefault(candidate => candidate.ColumnNumber == columnNumber);
            if (column == null)
            {
                throw new MatchActionException("The selected grouping refers to an unknown board column.");
            }
            return column;
        }
    }
}
