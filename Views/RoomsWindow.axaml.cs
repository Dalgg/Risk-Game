using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;

namespace RiskGame.Views
{
    /// <summary>
    /// Ventana de salas de juego. Muestra la lista de partidas disponibles y las acciones para
    /// unirse o crear una nueva sala.
    /// </summary>
    public partial class RoomsWindow : Window
    {
        /// <summary>
        /// Crea la ventana de salas y carga sus componentes visuales.
        /// </summary>
        public RoomsWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Regresa a la ventana principal.
        /// </summary>
        /// <param name="sender">Elemento que originó el evento.</param>
        /// <param name="e">Datos asociados al evento de interacción.</param>
        private void OnBackClick(object? sender, RoutedEventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                MainWindow mainWindow = new MainWindow();
                desktopLifetime.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
        }
    }
}