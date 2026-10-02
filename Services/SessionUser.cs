namespace RiskGame.Services
{
    /// <summary>
    /// Proyección inmutable y de solo lectura de un usuario autenticado, desacoplada de la entidad
    /// de Entity Framework para evitar exponer el modelo de datos a las capas de interfaz.
    /// </summary>
    /// <param name="IdUser">Identificador único del usuario dentro de la base de datos.</param>
    /// <param name="Username">Nombre de usuario único con el que se inició sesión.</param>
    /// <param name="Nickname">Apodo público mostrado en la interfaz del juego.</param>
    /// <param name="AvatarReference">
    /// Referencia o ruta del avatar del usuario, o <c>null</c> si todavía no tiene uno asignado.
    /// </param>
    public sealed record SessionUser(
        int IdUser,
        string Username,
        string Nickname,
        string? AvatarReference);
}