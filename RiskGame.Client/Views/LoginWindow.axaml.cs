using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class LoginWindow : Window
    {
        private static readonly IBrush ErrorBrush = new ImmutableSolidColorBrush(Color.Parse("#FF8A8A"));
        private static readonly IBrush SuccessBrush = new ImmutableSolidColorBrush(Color.Parse("#8CE99A"));

        public LoginWindow()
        {
            InitializeComponent();
        }

        public LoginWindow(string successMessage) : this()
        {
            ShowMessage(successMessage, SuccessBrush);
        }

        public LoginWindow(string message, bool isError) : this()
        {
            IBrush messageBrush = SuccessBrush;
            if (isError)
            {
                messageBrush = ErrorBrush;
            }
            ShowMessage(message, messageBrush);
        }

        private void RegisterClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            RegisterWindow registerWindow = new RegisterWindow();
            registerWindow.Show();
            this.Close();
        }

        private void ForgotPasswordClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            ForgotPasswordWindow forgotPasswordWindow = new ForgotPasswordWindow();
            if (Application.Current != null
                && Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                desktopLifetime.MainWindow = forgotPasswordWindow;
            }
            forgotPasswordWindow.Show();
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
            }
            else
            {
                AuthenticateAndNavigate(username, password);
            }
        }

        private void AuthenticateAndNavigate(string username, string password)
        {
            LoginViewModel loginViewModel = new LoginViewModel();
            try
            {
                SessionUser? authenticatedUser = loginViewModel.AuthenticateUser(username, password);
                if (authenticatedUser == null)
                {
                    ShowMessage(Strings.LoginErrorInvalidAuth, ErrorBrush);
                }
                else
                {
                    SessionService.Login(authenticatedUser);
                    SessionService.StartSessionConnection();
                    OpenMainWindow();
                }
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not sign in the requested player.");
                ShowMessage(dataOperationException.Message, ErrorBrush);
            }
        }

        private void OpenMainWindow()
        {
            if (Application.Current != null &&
                Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                MainWindow mainWindow = new MainWindow();
                desktopLifetime.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
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
