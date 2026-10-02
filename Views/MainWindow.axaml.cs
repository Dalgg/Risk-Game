using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    /// <summary>
    /// Ventana principal del juego. Actúa como menú de navegación entre las demás ventanas
    /// y refleja el estado de la sesión del jugador.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Crea la ventana principal y enlaza su modelo de vista.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        /// <summary>
        /// Sustituye la ventana principal activa por la ventana indicada.
        /// </summary>
        /// <param name="newWindow">Ventana que pasará a ser la ventana principal de la aplicación.</param>
        private void ChangeWindow(Window newWindow)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                desktopLifetime.MainWindow = newWindow;
                newWindow.Show();
                Close();
            }
        }

        /// <summary>
        /// Abre la ventana de configuración.
        /// </summary>
        /// <param name="sender">Elemento que originó el evento.</param>
        /// <param name="e">Datos asociados al evento de interacción.</param>
        private void OnSettingsClick(object? sender, RoutedEventArgs e)
        {
            ChangeWindow(new SettingsWindow());
        }

        /// <summary>
        /// Abre la ventana de inicio de sesión.
        /// </summary>
        /// <param name="sender">Elemento que originó el evento.</param>
        /// <param name="e">Datos asociados al evento de interacción.</param>
        private void OnLoginClick(object? sender, RoutedEventArgs e)
        {
            ChangeWindow(new LoginWindow());
        }

        /// <summary>
        /// Abre la ventana de salas.
        /// </summary>
        /// <param name="sender">Elemento que originó el evento.</param>
        /// <param name="e">Datos asociados al evento de interacción.</param>
        private void OnRoomsClick(object? sender, RoutedEventArgs e)
        {
            ChangeWindow(new RoomsWindow());
        }

        /// <summary>
        /// Abre la ventana de clasificación global.
        /// </summary>
        /// <param name="sender">Elemento que originó el evento.</param>
        /// <param name="e">Datos asociados al evento de interacción.</param>
        private void OnLeaderboardClick(object? sender, RoutedEventArgs e)
        {
            ChangeWindow(new LeaderboardWindow());
        }

        /// <summary>
        /// Cierra la aplicación.
        /// </summary>
        /// <param name="sender">Elemento que originó el evento.</param>
        /// <param name="e">Datos asociados al evento de interacción.</param>
        private void OnExitClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Cierra la sesión del jugador activo.
        /// </summary>
        /// <param name="sender">Elemento que originó el evento.</param>
        /// <param name="e">Datos asociados al evento de interacción.</param>
        private void OnProfileClick(object? sender, RoutedEventArgs e)
        {
            // TODO: Sustituir por la ventana de perfil; mientras tanto el botón cierra la sesión.
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.Logout();
            }
        }
    }
}