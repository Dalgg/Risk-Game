namespace RiskGame.Services
{
    public static class SessionService
    {
        public static SessionUser? CurrentUser { get; private set; }

        public static bool IsLoggedIn => CurrentUser != null;

        public static void Login(SessionUser user)
        {
            CurrentUser = user;
        }

        public static void Login(int idUser, string username, string nickname, string? avatarReference)
        {
            CurrentUser = new SessionUser(idUser, username, nickname, avatarReference);
        }

        public static void Logout()
        {
            CurrentUser = null;
        }
    }
}