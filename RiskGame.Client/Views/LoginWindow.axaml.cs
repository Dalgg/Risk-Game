using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using RiskGame.Exceptions;
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

        private void RegisterClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            RegisterWindow registerWindow = new RegisterWindow();
            registerWindow.Show();
            this.Close();
        }

        private void BackClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (Application.Current != null &&
                Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                MainWindow mainWindow = new MainWindow();
                desktopLifetime.MainWindow = mainWindow;
                mainWindow.Show();
                this.Close();
            }
        }

        private void OnConfirmLoginClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            string username = string.Empty;
            if (UserTextBox.Text != null)
            {
                username = UserTextBox.Text.Trim();
            }

            string password = string.Empty;
            if (PasswordTextBox.Text != null)
            {
                password = PasswordTextBox.Text;
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowMessage(Strings.LoginErrorEmptyFields, ErrorBrush);
                return;
            }

            LoginViewModel loginViewModel = new LoginViewModel();
            SessionUser? authenticatedUser;
            try
            {
                authenticatedUser = loginViewModel.AuthenticateUser(username, password);
            }
            catch (DataOperationException dataOperationException)
            {
                ShowMessage(dataOperationException.Message, ErrorBrush);
                return;
            }

            if (authenticatedUser == null)
            {
                ShowMessage(Strings.LoginErrorInvalidAuth, ErrorBrush);
                return;
            }

            SessionService.Login(authenticatedUser);

            if (Application.Current != null &&
                Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                MainWindow mainWindow = new MainWindow();
                desktopLifetime.MainWindow = mainWindow;
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