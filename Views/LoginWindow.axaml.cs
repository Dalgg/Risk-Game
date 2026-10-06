using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using RiskGame.Models;
using RiskGame.Resources;
using RiskGame.Services;
using RiskGame.Exceptions;
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
            string username = (UserTextBox.Text ?? string.Empty).Trim();
            string password = PasswordTextBox.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowMessage(Strings.LoginErrorEmptyFields, ErrorBrush);
                return;
            }

            LoginViewModel viewModel = new LoginViewModel();
            User? user;
            try
            {
                user = viewModel.AuthenticateUser(username, password);
            }
            catch (DataOperationException ex)
            {
                ShowMessage(ex.Message, ErrorBrush);
                return;
            }

            if (user == null)
            {
                ShowMessage(Strings.LoginErrorInvalidAuth, ErrorBrush);
                return;
            }

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