using System.Collections.ObjectModel;
using Grpc.Core;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public sealed record PlayerScoreView
    {
        public int Rank { get; init; }

        public string Username { get; init; } = string.Empty;

        public int TotalScore { get; init; }
    }

    public class LeaderboardViewModel : ViewModelBase
    {
        private bool _isDescending;

        public LeaderboardViewModel()
        {
        }

        public ObservableCollection<PlayerScoreView> Players { get; } = new ObservableCollection<PlayerScoreView>();

        public void LoadGlobalScores()
        {
            try
            {
                _ = GrpcChannelProvider.GetChannel();
                Players.Clear();
            }
            catch (RpcException ex)
            {
                JsonLogger.Error(ex, "gRPC communication error (RpcException) while loading leaderboard.");
                throw new DataOperationException(Strings.LeaderboardErrorDatabase, ex);
            }
        }

        public void ToggleSortDirection()
        {
            _isDescending = !_isDescending;
            LoadGlobalScores();
        }
    }
}
