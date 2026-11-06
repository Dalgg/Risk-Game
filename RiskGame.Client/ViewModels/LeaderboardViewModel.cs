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
        private bool isDescending;

        public LeaderboardViewModel()
        {
        }

        public ObservableCollection<PlayerScoreView> Players { get; } = new ObservableCollection<PlayerScoreView>();

        public void LoadGlobalScores()
        {
            try
            {
                GrpcChannelProvider.GetChannel();
                Players.Clear();
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "gRPC communication error (RpcException) while loading leaderboard.");
                throw new DataOperationException(Strings.LeaderboardErrorDatabase, rpcException);
            }
        }

        public void ToggleSortDirection()
        {
            isDescending = !isDescending;
            LoadGlobalScores();
        }
    }
}
