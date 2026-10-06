using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using RiskGame.Models;
using RiskGame.Resources;
using RiskGame.Services;
using RiskGame.ViewModels;

namespace RiskGame.Views
{

    public partial class LoginWindow : Window
    {
        private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#FF8A8A"));
        private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#8CE99A"));

        public LoginWindow()
        {
            InitializeComponent();
        }

        public LoginWindow(string successMessage) : this()
        {
            ShowMessage(successMessage, SuccessBrush);
        }

        private void OnRegisterClick(object? sender, RoutedEventArgs e)
        {
            RegisterWindow registerWindow = new RegisterWindow();
            registerWindow.Show();
            this.Close();
        }

        private void OnBackClick(object? sender, RoutedEventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                MainWindow mainWindow = new MainWindow();
                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                this.Close(); 
            }
        }

        private void OnConfirmLoginClick(object? sender, RoutedEventArgs e)
        {
            // 1. Obtener textos de la interfaz
            string username = (UserTextBox.Text ?? string.Empty).Trim();
            string password = PasswordTextBox.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowMessage(Strings.LoginErrorEmptyFields, ErrorBrush);
                return;
            }

            // 2. Validar con la base de datos
            var viewModel = new LoginViewModel();
            User? user = viewModel.AuthenticateUser(username, password);

            if (user == null)
            {
                ShowMessage(Strings.LoginErrorInvalidAuth, ErrorBrush);
                return;
            }

            // 3. Guardar la sesión; MainWindow la lee al crearse
            SessionService.Login(user);

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                MainWindow mainWindow = new MainWindow();
                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                this.Close();
            }
        }

        private void ShowMessage(string text, IBrush brush)
        {
            MessageTextBlock.Text = text;
            MessageTextBlock.Foreground = brush;
            MessageTextBlock.IsVisible = true;
        }
    }
}