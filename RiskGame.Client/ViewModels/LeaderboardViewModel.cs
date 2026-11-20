using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Contracts.Scoreboard;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public sealed partial class LeaderboardViewModel : ViewModelBase
    {
        private const int TopPlayersLimit = 20;
        private const int RequestTimeoutSeconds = 10;
        private const int EmptyPlayerCount = 0;
        private const string AuthorizationHeader = "authorization";
        private const string BearerPrefix = "Bearer ";
        private readonly SessionUser session;
        private readonly LeaderboardNavigationService navigation;
        private bool friendsOnly;
        private bool descending = true;
        private bool hasClosed;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private string scopeLabel = Strings.LeaderboardGlobalButton;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
        [NotifyCanExecuteChangedFor(nameof(GlobalCommand))]
        [NotifyCanExecuteChangedFor(nameof(FriendsCommand))]
        [NotifyCanExecuteChangedFor(nameof(SortCommand))]
        private bool isLoading;

        public LeaderboardViewModel(SessionUser session, LeaderboardNavigationService navigation)
        {
            this.session = session;
            this.navigation = navigation;
        }

        public ObservableCollection<ScoreEntry> Players { get; } = new ObservableCollection<ScoreEntry>();

        public void Deactivate()
        {
            hasClosed = true;
        }

        private bool CanQuery()
        {
            return !IsLoading && !hasClosed;
        }

        [RelayCommand(CanExecute = nameof(CanQuery))]
        private void Global()
        {
            friendsOnly = false;
            ScopeLabel = Strings.LeaderboardGlobalButton;
            Refresh();
        }

        [RelayCommand(CanExecute = nameof(CanQuery))]
        private void Friends()
        {
            friendsOnly = true;
            ScopeLabel = Strings.LeaderboardFriendsButton;
            Refresh();
        }

        [RelayCommand(CanExecute = nameof(CanQuery))]
        private void Sort()
        {
            descending = !descending;
            Refresh();
        }

        [RelayCommand(CanExecute = nameof(CanQuery))]
        private void Refresh()
        {
            IsLoading = true;
            Players.Clear();
            StatusMessage = Strings.LeaderboardLoadingLabel;
            ScoreQuery request = new ScoreQuery
            {
                TopLimit = TopPlayersLimit,
                FriendsOnly = friendsOnly,
                Descending = descending
            };
            Task.Run(() => LoadScores(request));
        }

        [RelayCommand]
        private void Back()
        {
            Deactivate();
            navigation.ReturnToMenu();
        }

        private void LoadScores(ScoreQuery request)
        {
            try
            {
                ScoreList scores = RequestScores(request);
                Dispatcher.UIThread.Post(() => ApplyScores(scores));
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Could not load the selected scoreboard.");
                Dispatcher.UIThread.Post(() => ApplyFailure(rpcException.StatusCode));
            }
        }

        private ScoreList RequestScores(ScoreQuery request)
        {
            Metadata headers = new Metadata();
            headers.Add(AuthorizationHeader, BearerPrefix + session.SessionToken);
            GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
            Scoreboard.ScoreboardClient client = new Scoreboard.ScoreboardClient(grpcChannel);
            return client.GetScores(request, headers: headers,
                deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
        }

        private void ApplyScores(ScoreList scores)
        {
            if (!hasClosed)
            {
                foreach (ScoreEntry player in scores.Players)
                {
                    Players.Add(player);
                }
                StatusMessage = string.Empty;
                if (Players.Count == EmptyPlayerCount)
                {
                    StatusMessage = Strings.LeaderboardEmptyLabel;
                }
                IsLoading = false;
            }
        }

        private void ApplyFailure(StatusCode statusCode)
        {
            if (!hasClosed)
            {
                IsLoading = false;
                bool needsLogin = statusCode == StatusCode.Unauthenticated || statusCode == StatusCode.PermissionDenied;
                if (needsLogin)
                {
                    Deactivate();
                    navigation.OpenLogin();
                }
                else
                {
                    StatusMessage = Strings.LeaderboardErrorDatabase;
                }
            }
        }
    }
}