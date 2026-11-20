using System;
using RiskGame.Contracts.Match;
using RiskGame.Resources;

namespace RiskGame.ViewModels
{
    public sealed class MatchCountdownViewModel : ViewModelBase
    {
        private const int NoSeconds = 0;
        private readonly TimeProvider timeProvider;
        private long receivedTimestamp;
        private TimeSpan remainingAtReceipt;
        private int matchId;
        private long turnId;
        private bool hasSnapshot;
        private int remainingSeconds;
        private bool isVisible;

        public MatchCountdownViewModel() : this(TimeProvider.System)
        {
        }

        public MatchCountdownViewModel(TimeProvider timeProvider)
        {
            this.timeProvider = timeProvider;
        }

        public int RemainingSeconds
        {
            get => remainingSeconds;
            private set
            {
                if (SetProperty(ref remainingSeconds, value))
                {
                    OnPropertyChanged(nameof(Text));
                    OnPropertyChanged(nameof(HasTimeRemaining));
                }
            }
        }

        public string Text => string.Format(Strings.MatchTimeRemainingFormat, RemainingSeconds);

        public bool HasTimeRemaining => IsVisible && RemainingSeconds > NoSeconds;

        public bool IsVisible
        {
            get => isVisible;
            private set
            {
                if (SetProperty(ref isVisible, value))
                {
                    OnPropertyChanged(nameof(HasTimeRemaining));
                }
            }
        }

        public void ApplySnapshot(BoardState snapshot)
        {
            TimeSpan receivedRemaining = TimeSpan.FromMilliseconds(snapshot.TurnRemainingMilliseconds);
            bool isSameTurn = hasSnapshot && snapshot.MatchId == matchId && snapshot.TurnId == turnId;
            if (isSameTurn && receivedRemaining > CalculateRemaining())
            {
                receivedRemaining = CalculateRemaining();
            }
            remainingAtReceipt = receivedRemaining;
            receivedTimestamp = timeProvider.GetTimestamp();
            matchId = snapshot.MatchId;
            turnId = snapshot.TurnId;
            hasSnapshot = true;
            IsVisible = !snapshot.IsFinished;
            Refresh();
        }

        public void Refresh()
        {
            RemainingSeconds = (int)Math.Ceiling(CalculateRemaining().TotalSeconds);
        }

        private TimeSpan CalculateRemaining()
        {
            TimeSpan remaining = TimeSpan.Zero;
            if (hasSnapshot && IsVisible)
            {
                TimeSpan elapsed = timeProvider.GetElapsedTime(receivedTimestamp);
                remaining = remainingAtReceipt - elapsed;
                if (remaining < TimeSpan.Zero)
                {
                    remaining = TimeSpan.Zero;
                }
            }
            return remaining;
        }
    }
}
