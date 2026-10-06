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
                    ResourceManager resourceManagerInstance = new ResourceManager("Risk-Game.Resources.Strings", typeof(Strings).Assembly);
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
    }
}