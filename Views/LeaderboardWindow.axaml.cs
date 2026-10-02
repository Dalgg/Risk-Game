using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    /// <summary>
    /// Ventana de clasificación global. Permite alternar la dirección del ordenamiento y recargar
    /// los resultados de la tabla de mejores jugadores.
    /// </summary>
    public partial class LeaderboardWindow : Window
    {
        /// <summary>
        /// Modelo de vista que alimenta la tabla de clasificación.
        /// </summary>
        private readonly LeaderboardViewModel _viewModel;

        /// <summary>
        /// Crea la ventana de clasificación y enlaza su modelo de vista.
        /// </summary>
        public LeaderboardWindow()
        {
            InitializeComponent();

            _viewModel = new LeaderboardViewModel();
            DataContext = _viewModel;
        }

        /// <summary>
        /// Invierte la dirección del ordenamiento de la tabla.
        /// </summary>
        /// <param name="sender">Elemento que originó el evento.</param>
        /// <param name="e">Datos asociados al evento de interacción.</param>
        private void OnSortClick(object? sender, RoutedEventArgs e)
        {
            _viewModel.ToggleSortDirection();
        }

        /// <summary>
        /// Vuelve a consultar los diez mejores resultados globales.
        /// </summary>
        /// <param name="sender">Elemento que originó el evento.</param>
        /// <param name="e">Datos asociados al evento de interacción.</param>
        private void OnGlobalClick(object? sender, RoutedEventArgs e)
        {
            _viewModel.LoadGlobalScores();
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