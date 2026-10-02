using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.Models;
using RiskGame.Services;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    /// <summary>
    /// Ventana de inicio de sesión. Valida las credenciales tecleadas por el jugador y, cuando
    /// son válidas, establece la sesión antes de volver a la ventana principal.
    /// </summary>
    public partial class LoginWindow : Window
    {
        /// <summary>
        /// Crea la ventana de inicio de sesión y carga sus componentes visuales.
        /// </summary>
        public LoginWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Abre la ventana de registro de cuenta.
        /// </summary>
        /// <param name="sender">Elemento que originó el evento.</param>
        /// <param name="e">Datos asociados al evento de interacción.</param>
        private void OnRegisterClick(object? sender, RoutedEventArgs e)
        {
            RegisterWindow registerWindow = new RegisterWindow();
            registerWindow.Show();
            Close();
        }

        /// <summary>
        /// Regresa a la ventana principal sin iniciar sesión.
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

        /// <summary>
        /// Autentica al jugador con las credenciales capturadas y abre la ventana principal si el
        /// acceso es válido.
        /// </summary>
        /// <param name="sender">Elemento que originó el evento.</param>
        /// <param name="e">Datos asociados al evento de interacción.</param>
        private void OnConfirmLoginClick(object? sender, RoutedEventArgs e)
        {
            string username = UserTextBox.Text ?? string.Empty;
            string password = PasswordTextBox.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("Error: Faltan credenciales.");
                return;
            }

            LoginViewModel viewModel = new LoginViewModel();
            User? user = viewModel.AuthenticateUser(username, password);

            if (user == null)
            {
                // TODO: Mostrar un TextBlock rojo con el mensaje "Usuario o contraseña incorrectos".
                Console.WriteLine("Acceso denegado: Usuario o contraseña incorrectos.");
                return;
            }

            SessionService.Login(user);
            Console.WriteLine("Acceso autorizado.");

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