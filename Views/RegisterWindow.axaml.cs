using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.ViewModels;

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
            Console.WriteLine("PRUEBA: Faltan campos por llenar.");
            return;
        }

        RegisterViewModel viewModel = new RegisterViewModel();
        var resultado = viewModel.RegisterUser(email, usuario, password);

        if (resultado.Success)
        {
            Console.WriteLine($"PRUEBA EXITOSA: {resultado.Message}");
        }
        else
        {
            Console.WriteLine($"PRUEBA FALLIDA: {resultado.Message}");
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
