using RiskGame.Models;

namespace RiskGame.Services
{
    /// <summary>
    /// Mantiene en memoria los datos del usuario que inició sesión mientras la aplicación está en ejecución.
    /// </summary>
    public static class SessionService
    {
        /// <summary>
        /// Usuario actualmente autenticado, o <c>null</c> si no hay ninguna sesión activa.
        /// </summary>
        public static SessionUser? CurrentUser { get; private set; }

        /// <summary>
        /// Indica si existe una sesión activa en la aplicación.
        /// </summary>
        public static bool IsLoggedIn => CurrentUser != null;

        /// <summary>
        /// Inicia la sesión con el usuario indicado y proyecta sus datos públicos como
        /// <see cref="SessionUser"/> para que las capas superiores no dependan del modelo de datos.
        /// </summary>
        /// <param name="user">
        /// Entidad de usuario previamente validada contra la base de datos durante el inicio de sesión.
        /// </param>
        public static void Login(User user)
        {
            CurrentUser = new SessionUser(
                user.IdUser,
                user.Username,
                user.Nickname,
                user.AvatarReference);
        }

        /// <summary>
        /// Cierra la sesión activa y descarta de memoria los datos del usuario autenticado.
        /// </summary>
        public static void Logout()
        {
            CurrentUser = null;
        }
    }
}