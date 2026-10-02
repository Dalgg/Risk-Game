using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    /// <summary>
    /// Ventana de registro de cuenta. Captura los datos del nuevo jugador y delega su
    /// persistencia en <see cref="RegisterViewModel"/>.
    /// </summary>
    public partial class RegisterWindow : Window
    {
        /// <summary>
        /// Crea la ventana de registro y carga sus componentes visuales.
        /// </summary>
        public RegisterWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Valida los datos capturados y solicita la creación de la cuenta.
        /// </summary>
        /// <param name="sender">Elemento que originó el evento.</param>
        /// <param name="e">Datos asociados al evento de interacción.</param>
        private void OnConfirmClick(object? sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text ?? string.Empty;
            string username = UsernameTextBox.Text ?? string.Empty;
            string password = PasswordTextBox.Text ?? string.Empty;

            bool hasMissingField = string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(username)
                || string.IsNullOrWhiteSpace(password);

            if (hasMissingField)
            {
                // TODO: Mostrar un diálogo de error cuando exista el servicio de diálogos de la interfaz.
                return;
            }

            RegisterViewModel viewModel = new RegisterViewModel();
            (bool Success, string Message) result = viewModel.RegisterUser(email, username, password);

            // TODO: Mostrar un diálogo de éxito o de error con el mensaje devuelto por el modelo de vista.
        }

        /// <summary>
        /// Regresa a la ventana de inicio de sesión.
        /// </summary>
        /// <param name="sender">Elemento que originó el evento.</param>
        /// <param name="e">Datos asociados al evento de interacción.</param>
        private void OnBackClick(object? sender, RoutedEventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                LoginWindow loginWindow = new LoginWindow();
                desktopLifetime.MainWindow = loginWindow;
                loginWindow.Show();
                Close();
            }
        }
    }
}