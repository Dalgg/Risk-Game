using CommunityToolkit.Mvvm.ComponentModel;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    /// <summary>
    /// Modelo de vista de la ventana principal. Refleja el estado de la sesión activa y expone
    /// los datos del usuario que la interfaz necesita mostrar.
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        /// <summary>
        /// Indica si existe una sesión activa en la aplicación.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGuest))]
        private bool _isLoggedIn;

        /// <summary>
        /// Nombre de usuario del jugador autenticado, o una cadena vacía si la sesión está cerrada.
        /// </summary>
        [ObservableProperty]
        private string _loggedUsername = string.Empty;

        /// <summary>
        /// Referencia del avatar del jugador autenticado, o <c>null</c> si no tiene uno asignado.
        /// </summary>
        [ObservableProperty]
        private string? _avatarPath;

        /// <summary>
        /// Crea el modelo de vista y sincroniza sus propiedades con la sesión vigente.
        /// </summary>
        public MainViewModel()
        {
            RefreshFromSession();
        }

        /// <summary>
        /// Indica que la interfaz debe presentarse en modo invitado.
        /// </summary>
        public bool IsGuest => !IsLoggedIn;

        /// <summary>
        /// Cierra la sesión del usuario activo y actualiza la interfaz en consecuencia.
        /// </summary>
        public void Logout()
        {
            SessionService.Logout();
            RefreshFromSession();
        }

        /// <summary>
        /// Vuelca en las propiedades del modelo de vista los datos del usuario de la sesión actual.
        /// </summary>
        private void RefreshFromSession()
        {
            SessionUser? user = SessionService.CurrentUser;

            IsLoggedIn = user != null;
            LoggedUsername = user?.Username ?? string.Empty;
            AvatarPath = user?.AvatarReference;
        }
    }
}