using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.ViewModels;
using System;

namespace RiskGame.Views
{
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();
        }

        private void OnConfirmClick(object? sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text ?? string.Empty;
            string usuario = UsernameTextBox.Text ?? string.Empty;
            string password = PasswordTextBox.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            {
                // De acuerdo al estandar, evitamos Console.WriteLine.
                // Como esto es un UI, idealmente usariamos un MessageBox, pero para no romper 
                // dependencias lo comentamos hasta que implementen dialogos.
                return;
            }

            RegisterViewModel viewModel = new RegisterViewModel();
            (bool Success, string Message) resultado = viewModel.RegisterUser(email, usuario, password);

            if (resultado.Success)
            {
                // Aqui iria un ILogger o Dialogo de exito
            }
            else
            {
                // Aqui iria un ILogger o Dialogo de error
            }
        }

        private void OnBackClick(object? sender, RoutedEventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                LoginWindow loginWindow = new LoginWindow();
                desktop.MainWindow = loginWindow;
                loginWindow.Show();
                this.Close();
            }
        }
    }
}
