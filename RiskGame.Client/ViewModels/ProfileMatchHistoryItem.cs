using System;
using System.Globalization;
using RiskGame.Contracts.Profile;
using RiskGame.Resources;

namespace RiskGame.ViewModels
{
    public sealed class ProfileMatchHistoryItem
    {
        private const string VictoryEndReason = "Victory";
        private const string AbandonmentEndReason = "Abandonment";
        private const string CancelledEndReason = "Cancelled";
        private const string LocalDateTimeFormat = "g";

        public ProfileMatchHistoryItem(RecentMatchResult result)
        {
            MatchText = string.Format(Strings.ProfileHistoryMatchFormat, result.MatchId);
            DateTimeOffset endedAt = DateTimeOffset.FromUnixTimeSeconds(result.EndTimeUnixSeconds).ToLocalTime();
            DateText = string.Format(Strings.ProfileHistoryDateFormat,
                endedAt.ToString(LocalDateTimeFormat, CultureInfo.CurrentCulture));
            OutcomeText = GetOutcomeText(result);
            string columns = Strings.ProfileStatisticUnavailable;
            if (result.HasClaimedColumns)
            {
                columns = result.ClaimedColumns.ToString(CultureInfo.CurrentCulture);
            }
            ColumnsText = string.Format(Strings.ProfileHistoryColumnsFormat, columns);
            CompletionText = GetCompletionText(result.EndReason);
        }

        public string MatchText { get; }

        public string DateText { get; }

        public string OutcomeText { get; }

        public string ColumnsText { get; }

        public string CompletionText { get; }

        private static string GetOutcomeText(RecentMatchResult result)
        {
            string outcome = Strings.ProfileHistoryResultUnavailable;
            if (result.HasResult)
            {
                outcome = Strings.ProfileHistoryDefeat;
                if (result.IsWinner)
                {
                    outcome = Strings.ProfileHistoryVictory;
                }
            }
            if (result.EndReason == CancelledEndReason)
            {
                outcome = Strings.ProfileHistoryCancelled;
            }
            return outcome;
        }

        private static string GetCompletionText(string endReason)
        {
            string completion = Strings.ProfileHistoryCompleted;
            switch (endReason)
            {
                case VictoryEndReason:
                    completion = Strings.ProfileHistoryColumnVictory;
                    break;
                case AbandonmentEndReason:
                    completion = Strings.ProfileHistoryAbandonment;
                    break;
                case CancelledEndReason:
                    completion = Strings.ProfileHistoryCancelled;
                    break;
                default:
                    break;
            }
            return completion;
        }
    }
}
