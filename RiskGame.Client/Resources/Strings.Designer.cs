using System;
using System.Globalization;
using System.Resources;

namespace RiskGame.Resources
{

    public class Strings
    {
        private static ResourceManager _resourceMan;
        private static CultureInfo _resourceCulture;
        
        public static ResourceManager ResourceManager
        {
            get
            {
                if (object.ReferenceEquals(_resourceMan, null))
                {
                    ResourceManager resourceManagerInstance = new ResourceManager("RiskGame.Resources.Strings", typeof(Strings).Assembly);
                    _resourceMan = resourceManagerInstance;
                }
                return _resourceMan;
            }
        }
        
        public static CultureInfo Culture
        {
            get
            {
                return _resourceCulture;
            }
            set
            {
                _resourceCulture = value;
            }
        }
        
        public static string LoginTitleLabel => ResourceManager.GetString("LoginTitleLabel", _resourceCulture);

        public static string LoginUsernameLabel => ResourceManager.GetString("LoginUsernameLabel", _resourceCulture);

        public static string LoginPasswordLabel => ResourceManager.GetString("LoginPasswordLabel", _resourceCulture);

        public static string LoginConfirmButton => ResourceManager.GetString("LoginConfirmButton", _resourceCulture);

        public static string LoginRegisterButton => ResourceManager.GetString("LoginRegisterButton", _resourceCulture);

        public static string LoginErrorInvalidAuth => ResourceManager.GetString("LoginErrorInvalidAuth", _resourceCulture);

        public static string LoginErrorEmptyFields => ResourceManager.GetString("LoginErrorEmptyFields", _resourceCulture);
        
        public static string RegisterTitleLabel => ResourceManager.GetString("RegisterTitleLabel", _resourceCulture);

        public static string RegisterEmailLabel => ResourceManager.GetString("RegisterEmailLabel", _resourceCulture);

        public static string RegisterConfirmButton => ResourceManager.GetString("RegisterConfirmButton", _resourceCulture);

        public static string RegisterErrorUserExists => ResourceManager.GetString("RegisterErrorUserExists", _resourceCulture);

        public static string RegisterErrorEmptyFields => ResourceManager.GetString("RegisterErrorEmptyFields", _resourceCulture);

        public static string RegisterErrorInvalidEmail => ResourceManager.GetString("RegisterErrorInvalidEmail", _resourceCulture);

        public static string RegisterErrorInvalidUsername => ResourceManager.GetString("RegisterErrorInvalidUsername", _resourceCulture);

        public static string RegisterErrorWeakPassword => ResourceManager.GetString("RegisterErrorWeakPassword", _resourceCulture);

        public static string RegisterErrorEmailExists => ResourceManager.GetString("RegisterErrorEmailExists", _resourceCulture);

        public static string RegisterSuccess => ResourceManager.GetString("RegisterSuccess", _resourceCulture);
        
        public static string MainMenuStartButton => ResourceManager.GetString("MainMenuStartButton", _resourceCulture);

        public static string MainMenuRoomsButton => ResourceManager.GetString("MainMenuRoomsButton", _resourceCulture);

        public static string MainMenuLeaderboardButton => ResourceManager.GetString("MainMenuLeaderboardButton", _resourceCulture);

        public static string MainMenuExitButton => ResourceManager.GetString("MainMenuExitButton", _resourceCulture);

        public static string MainMenuSinglePlayerButton => ResourceManager.GetString("MainMenuSinglePlayerButton", _resourceCulture);

        public static string MainMenuFriendsButton => ResourceManager.GetString("MainMenuFriendsButton", _resourceCulture);

        public static string MainMenuSettingsButton => ResourceManager.GetString("MainMenuSettingsButton", _resourceCulture);
        
        public static string RoomsTitleLabel => ResourceManager.GetString("RoomsTitleLabel", _resourceCulture);

        public static string RoomsPlayButton => ResourceManager.GetString("RoomsPlayButton", _resourceCulture);

        public static string RoomsErrorNotEnoughPlayers => ResourceManager.GetString("RoomsErrorNotEnoughPlayers", _resourceCulture);

        public static string RoomsCreateRoomButton => ResourceManager.GetString("RoomsCreateRoomButton", _resourceCulture);

        public static string RoomsJoinRoomButton => ResourceManager.GetString("RoomsJoinRoomButton", _resourceCulture);
        
        public static string GlobalBackButton => ResourceManager.GetString("GlobalBackButton", _resourceCulture);
        
        public static string GameRollButton => ResourceManager.GetString("GameRollButton", _resourceCulture);

        public static string GameStopButton => ResourceManager.GetString("GameStopButton", _resourceCulture);

        public static string GameStateBust => ResourceManager.GetString("GameStateBust", _resourceCulture);

        public static string GameStateClaim => ResourceManager.GetString("GameStateClaim", _resourceCulture);

        public static string GameInstructionPairing => ResourceManager.GetString("GameInstructionPairing", _resourceCulture);
        
        public static string ProfileGamesPlayedLabel => ResourceManager.GetString("ProfileGamesPlayedLabel", _resourceCulture);

        public static string ProfileWinsLabel => ResourceManager.GetString("ProfileWinsLabel", _resourceCulture);
        
        public static string SettingsTitleLabel => ResourceManager.GetString("SettingsTitleLabel", _resourceCulture);

        public static string SettingsLanguageLabel => ResourceManager.GetString("SettingsLanguageLabel", _resourceCulture);

        public static string SettingsLanguageSpanish => ResourceManager.GetString("SettingsLanguageSpanish", _resourceCulture);

        public static string SettingsLanguageEnglish => ResourceManager.GetString("SettingsLanguageEnglish", _resourceCulture);

        public static string SettingsVolumeLabel => ResourceManager.GetString("SettingsVolumeLabel", _resourceCulture);

        public static string SettingsBrightnessLabel => ResourceManager.GetString("SettingsBrightnessLabel", _resourceCulture);

        public static string LeaderboardTitle => ResourceManager.GetString("LeaderboardTitle", _resourceCulture);

        public static string LeaderboardSubtitle => ResourceManager.GetString("LeaderboardSubtitle", _resourceCulture);

        public static string LeaderboardGlobalButton => ResourceManager.GetString("LeaderboardGlobalButton", _resourceCulture);
        
        public static string LeaderboardFriendsButton => ResourceManager.GetString("LeaderboardFriendsButton", _resourceCulture);

        public static string RegisterErrorDataConflict => ResourceManager.GetString("RegisterErrorDataConflict", _resourceCulture);

        public static string RegisterErrorConnectionFailed => ResourceManager.GetString("RegisterErrorConnectionFailed", _resourceCulture);

        public static string LoginErrorDatabase => ResourceManager.GetString("LoginErrorDatabase", _resourceCulture);

        public static string LeaderboardErrorDatabase => ResourceManager.GetString("LeaderboardErrorDatabase", _resourceCulture);

        public static string RoomsCodeLabel => ResourceManager.GetString("RoomsCodeLabel", _resourceCulture);

        public static string RoomsErrorEmptyCode => ResourceManager.GetString("RoomsErrorEmptyCode", _resourceCulture);

        public static string LobbyTitleLabel => ResourceManager.GetString("LobbyTitleLabel", _resourceCulture);

        public static string LobbyRoomIdFormat => ResourceManager.GetString("LobbyRoomIdFormat", _resourceCulture);

        public static string LobbyPlayersHeaderLabel => ResourceManager.GetString("LobbyPlayersHeaderLabel", _resourceCulture);

        public static string LobbyFriendsHeaderLabel => ResourceManager.GetString("LobbyFriendsHeaderLabel", _resourceCulture);

        public static string LobbyLeaveButton => ResourceManager.GetString("LobbyLeaveButton", _resourceCulture);

        public static string LobbyOptionsButton => ResourceManager.GetString("LobbyOptionsButton", _resourceCulture);

        public static string LobbyPlayButton => ResourceManager.GetString("LobbyPlayButton", _resourceCulture);

        public static string LobbyDefaultPlayerName => ResourceManager.GetString("LobbyDefaultPlayerName", _resourceCulture);

        public static string LobbyHostRoleLabel => ResourceManager.GetString("LobbyHostRoleLabel", _resourceCulture);

        public static string LobbyConnectedRoleLabel => ResourceManager.GetString("LobbyConnectedRoleLabel", _resourceCulture);

        public static string LobbyErrorNotFound => ResourceManager.GetString("LobbyErrorNotFound", _resourceCulture);

        public static string LobbyErrorFull => ResourceManager.GetString("LobbyErrorFull", _resourceCulture);

        public static string LobbyErrorConnectionFailed => ResourceManager.GetString("LobbyErrorConnectionFailed", _resourceCulture);

        public static string LobbyChatHeaderLabel => ResourceManager.GetString("LobbyChatHeaderLabel", _resourceCulture);

        public static string LobbyChatPlaceholder => ResourceManager.GetString("LobbyChatPlaceholder", _resourceCulture);

        public static string LobbyChatSendButton => ResourceManager.GetString("LobbyChatSendButton", _resourceCulture);

        public static string LobbyChatMessageFormat => ResourceManager.GetString("LobbyChatMessageFormat", _resourceCulture);

        public static string MatchTitleLabel => ResourceManager.GetString("MatchTitleLabel", _resourceCulture);

        public static string MatchIdFormat => ResourceManager.GetString("MatchIdFormat", _resourceCulture);

        public static string MatchPlayersHeaderLabel => ResourceManager.GetString("MatchPlayersHeaderLabel", _resourceCulture);

        public static string MatchBoardPlaceholderLabel => ResourceManager.GetString("MatchBoardPlaceholderLabel", _resourceCulture);

        public static string MatchLeaveButton => ResourceManager.GetString("MatchLeaveButton", _resourceCulture);

        public static string LobbyInviteButton => ResourceManager.GetString("LobbyInviteButton", _resourceCulture);

        public static string LobbyInvitedButton => ResourceManager.GetString("LobbyInvitedButton", _resourceCulture);

        public static string LobbyNoFriendsMessage => ResourceManager.GetString("LobbyNoFriendsMessage", _resourceCulture);

        public static string LobbyFriendsGuestDisabled => ResourceManager.GetString("LobbyFriendsGuestDisabled", _resourceCulture);

        public static string LobbyFriendOnlineStatus => ResourceManager.GetString("LobbyFriendOnlineStatus", _resourceCulture);

        public static string LobbyFriendOfflineStatus => ResourceManager.GetString("LobbyFriendOfflineStatus", _resourceCulture);

        public static string GlobalGameTitle => ResourceManager.GetString("GlobalGameTitle", _resourceCulture);
        public static string ProfileTitle => ResourceManager.GetString("ProfileTitle", _resourceCulture);

        public static string ProfileIdentifier => ResourceManager.GetString("ProfileIdentifier", _resourceCulture);

        public static string ProfileMatchesPlayed => ResourceManager.GetString("ProfileMatchesPlayed", _resourceCulture);

        public static string ProfileMatchesWon => ResourceManager.GetString("ProfileMatchesWon", _resourceCulture);

        public static string ProfileBack => ResourceManager.GetString("ProfileBack", _resourceCulture);

        public static string ProfileChangePassword => ResourceManager.GetString("ProfileChangePassword", _resourceCulture);

        public static string ProfilePasswordPending => ResourceManager.GetString("ProfilePasswordPending", _resourceCulture);

        public static string ProfileLoadError => ResourceManager.GetString("ProfileLoadError", _resourceCulture);

        public static string ProfileRefresh => ResourceManager.GetString("ProfileRefresh", _resourceCulture);

        public static string ProfileStatisticUnavailable => ResourceManager.GetString("ProfileStatisticUnavailable", _resourceCulture);
        public static string ViewNotFound => ResourceManager.GetString("ViewNotFound", _resourceCulture);
        public static string FriendsTitle => ResourceManager.GetString("FriendsTitle", _resourceCulture);

        public static string FriendsEmpty => ResourceManager.GetString("FriendsEmpty", _resourceCulture);

        public static string FriendsLoading => ResourceManager.GetString("FriendsLoading", _resourceCulture);

        public static string FriendsLoadError => ResourceManager.GetString("FriendsLoadError", _resourceCulture);

        public static string FriendsUsernameRequired => ResourceManager.GetString("FriendsUsernameRequired", _resourceCulture);

        public static string FriendsSending => ResourceManager.GetString("FriendsSending", _resourceCulture);

        public static string FriendsRequestSent => ResourceManager.GetString("FriendsRequestSent", _resourceCulture);

        public static string FriendsRequestRejected => ResourceManager.GetString("FriendsRequestRejected", _resourceCulture);

        public static string FriendsLoginRequired => ResourceManager.GetString("FriendsLoginRequired", _resourceCulture);

        public static string FriendsUsernamePlaceholder => ResourceManager.GetString("FriendsUsernamePlaceholder", _resourceCulture);

        public static string FriendsAdd => ResourceManager.GetString("FriendsAdd", _resourceCulture);

        public static string InboxTitle => ResourceManager.GetString("InboxTitle", _resourceCulture);

        public static string InboxFriendRequests => ResourceManager.GetString("InboxFriendRequests", _resourceCulture);

        public static string InboxLoading => ResourceManager.GetString("InboxLoading", _resourceCulture);

        public static string InboxEmpty => ResourceManager.GetString("InboxEmpty", _resourceCulture);

        public static string InboxSelectRequest => ResourceManager.GetString("InboxSelectRequest", _resourceCulture);

        public static string InboxSaving => ResourceManager.GetString("InboxSaving", _resourceCulture);

        public static string InboxAccept => ResourceManager.GetString("InboxAccept", _resourceCulture);

        public static string InboxReject => ResourceManager.GetString("InboxReject", _resourceCulture);

        public static string InboxConnectionError => ResourceManager.GetString("InboxConnectionError", _resourceCulture);

        public static string InboxRequestUnavailable => ResourceManager.GetString("InboxRequestUnavailable", _resourceCulture);

        public static string InboxLobbyInvitations => ResourceManager.GetString("InboxLobbyInvitations", _resourceCulture);

        public static string InboxLobbyCode => ResourceManager.GetString("InboxLobbyCode", _resourceCulture);

        public static string InboxInvitationUnavailable => ResourceManager.GetString("InboxInvitationUnavailable", _resourceCulture);

    }
}
