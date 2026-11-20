using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Contracts.LobbySettings;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public sealed partial class LobbySettingsViewModel : ViewModelBase
    {
        private const int ShortTurnSeconds = 30;
        private const int DefaultTurnSeconds = 60;
        private const int LongTurnSeconds = 90;
        private const int RequestTimeoutSeconds = 10;
        private const int CollapsedWindowHeight = 720;
        private const int ExpandedWindowHeight = 920;
        private RulesVariant confirmedVariant;
        private int confirmedDuration;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WindowHeight))]
        private bool isExpanded;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private bool canEdit;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private string summary = string.Empty;

        [ObservableProperty]
        private string selectedVariant = Strings.LobbyStandardRules;

        [ObservableProperty]
        private int selectedDuration = DefaultTurnSeconds;

        public IReadOnlyList<string> Variants { get; } = new[]
        {
            Strings.LobbyStandardRules, Strings.LobbyFastRules
        };

        public IReadOnlyList<int> Durations { get; } = new[]
        {
            ShortTurnSeconds, DefaultTurnSeconds, LongTurnSeconds
        };

        public int WindowHeight
        {
            get
            {
                int height = CollapsedWindowHeight;
                if (IsExpanded)
                {
                    height = ExpandedWindowHeight;
                }
                return height;
            }
        }

        public void ApplySnapshot(LobbyDetails details)
        {
            SessionUser session = SessionService.GetOrCreateCurrentUser();
            bool editable = details.HostUserId == session.IdUser && details.Status == LobbyStatus.Waiting;
            bool rulesChanged = confirmedVariant != details.Rules.RulesVariant
                || confirmedDuration != details.Rules.TurnDurationSeconds;
            if (rulesChanged || CanEdit != editable)
            {
                SelectedVariant = GetVariantLabel(details.Rules.RulesVariant);
                SelectedDuration = details.Rules.TurnDurationSeconds;
                StatusMessage = string.Empty;
            }
            confirmedVariant = details.Rules.RulesVariant;
            confirmedDuration = details.Rules.TurnDurationSeconds;
            CanEdit = editable;
            Summary = string.Format(Strings.LobbyRulesSummary, GetVariantLabel(confirmedVariant), confirmedDuration);
        }

        [RelayCommand]
        private void ToggleSettings()
        {
            IsExpanded = !IsExpanded;
        }

        private bool CanSave()
        {
            return CanEdit;
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save()
        {
            try
            {
                ApplySnapshot(QueryUpdate());
                StatusMessage = Strings.LobbySettingsSaved;
            }
            catch (RpcException rpcException)
            {
                StatusMessage = Strings.LobbySettingsFailed;
                if (rpcException.StatusCode == StatusCode.Unavailable
                    || rpcException.StatusCode == StatusCode.DeadlineExceeded)
                {
                    JsonLogger.Error(rpcException, "Could not update the lobby settings.");
                }
            }
        }

        private LobbyDetails QueryUpdate()
        {
            SessionUser session = SessionService.GetOrCreateCurrentUser();
            RulesVariant variant = RulesVariant.Standard;
            if (SelectedVariant == Strings.LobbyFastRules)
            {
                variant = RulesVariant.Fast;
            }
            UpdateLobbyRulesRequest request = new UpdateLobbyRulesRequest
            {
                UserId = session.IdUser,
                SessionToken = session.SessionToken,
                Rules = new LobbyRules { RulesVariant = variant, TurnDurationSeconds = SelectedDuration }
            };
            GrpcChannel channel = GrpcChannelProvider.GetChannel();
            LobbySettings.LobbySettingsClient client = new LobbySettings.LobbySettingsClient(channel);
            return client.UpdateRules(request, deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
        }

        private static string GetVariantLabel(RulesVariant variant)
        {
            string label = Strings.LobbyStandardRules;
            if (variant == RulesVariant.Fast)
            {
                label = Strings.LobbyFastRules;
            }
            return label;
        }
    }
}
