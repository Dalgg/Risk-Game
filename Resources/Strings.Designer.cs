namespace Risk_Game.Resources {
    using System;
    public class Strings {
        private static global::System.Resources.ResourceManager resourceMan;
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Risk-Game.Resources.Strings", typeof(Strings).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        public static global::System.Globalization.CultureInfo Culture {
            get { return resourceCulture; }
            set { resourceCulture = value; }
        }
        
        public static string Login_TitleLabel => ResourceManager.GetString("Login_TitleLabel", resourceCulture);
        public static string Login_UsernameLabel => ResourceManager.GetString("Login_UsernameLabel", resourceCulture);
        public static string Login_PasswordLabel => ResourceManager.GetString("Login_PasswordLabel", resourceCulture);
        public static string Login_ConfirmButton => ResourceManager.GetString("Login_ConfirmButton", resourceCulture);
        public static string Login_RegisterButton => ResourceManager.GetString("Login_RegisterButton", resourceCulture);
        public static string Login_ErrorInvalidAuth => ResourceManager.GetString("Login_ErrorInvalidAuth", resourceCulture);
        
        public static string Register_TitleLabel => ResourceManager.GetString("Register_TitleLabel", resourceCulture);
        public static string Register_EmailLabel => ResourceManager.GetString("Register_EmailLabel", resourceCulture);
        public static string Register_ConfirmButton => ResourceManager.GetString("Register_ConfirmButton", resourceCulture);
        public static string Register_ErrorUserExists => ResourceManager.GetString("Register_ErrorUserExists", resourceCulture);
        
        public static string MainMenu_StartButton => ResourceManager.GetString("MainMenu_StartButton", resourceCulture);
        public static string MainMenu_RoomsButton => ResourceManager.GetString("MainMenu_RoomsButton", resourceCulture);
        public static string MainMenu_LeaderboardButton => ResourceManager.GetString("MainMenu_LeaderboardButton", resourceCulture);
        public static string MainMenu_ExitButton => ResourceManager.GetString("MainMenu_ExitButton", resourceCulture);
        public static string MainMenu_SinglePlayerButton => ResourceManager.GetString("MainMenu_SinglePlayerButton", resourceCulture);
        public static string MainMenu_FriendsButton => ResourceManager.GetString("MainMenu_FriendsButton", resourceCulture);
        public static string MainMenu_SettingsButton => ResourceManager.GetString("MainMenu_SettingsButton", resourceCulture);
        
        public static string Rooms_TitleLabel => ResourceManager.GetString("Rooms_TitleLabel", resourceCulture);
        public static string Rooms_PlayButton => ResourceManager.GetString("Rooms_PlayButton", resourceCulture);
        public static string Rooms_ErrorNotEnoughPlayers => ResourceManager.GetString("Rooms_ErrorNotEnoughPlayers", resourceCulture);
        public static string Rooms_CreateRoomButton => ResourceManager.GetString("Rooms_CreateRoomButton", resourceCulture);
        public static string Rooms_JoinRoomButton => ResourceManager.GetString("Rooms_JoinRoomButton", resourceCulture);
        
        public static string Global_BackButton => ResourceManager.GetString("Global_BackButton", resourceCulture);
        
        public static string Game_RollButton => ResourceManager.GetString("Game_RollButton", resourceCulture);
        public static string Game_StopButton => ResourceManager.GetString("Game_StopButton", resourceCulture);
        public static string Game_StateBust => ResourceManager.GetString("Game_StateBust", resourceCulture);
        public static string Game_StateClaim => ResourceManager.GetString("Game_StateClaim", resourceCulture);
        public static string Game_InstructionPairing => ResourceManager.GetString("Game_InstructionPairing", resourceCulture);
        
        public static string Profile_GamesPlayedLabel => ResourceManager.GetString("Profile_GamesPlayedLabel", resourceCulture);
        public static string Profile_WinsLabel => ResourceManager.GetString("Profile_WinsLabel", resourceCulture);
        
        public static string Settings_TitleLabel => ResourceManager.GetString("Settings_TitleLabel", resourceCulture);
        public static string Settings_LanguageLabel => ResourceManager.GetString("Settings_LanguageLabel", resourceCulture);
        public static string Settings_LanguageSpanish => ResourceManager.GetString("Settings_LanguageSpanish", resourceCulture);
        public static string Settings_LanguageEnglish => ResourceManager.GetString("Settings_LanguageEnglish", resourceCulture);
        public static string Settings_VolumeLabel => ResourceManager.GetString("Settings_VolumeLabel", resourceCulture);
        public static string Settings_BrightnessLabel => ResourceManager.GetString("Settings_BrightnessLabel", resourceCulture);
    }
}
