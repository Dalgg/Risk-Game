using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using RiskGame.Models;
using RiskGame.Resources;
using RiskGame.Exceptions;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class RegisterWindow : Window
    {
        private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#FF8A8A"));

        public RegisterWindow()
        {
            InitializeComponent();
        }

        private void OnConfirmClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            string email = string.Empty;
            if (EmailTextBox.Text != null)
            {
                email = EmailTextBox.Text;
            }

            string username = string.Empty;
            if (UsernameTextBox.Text != null)
            {
                username = UsernameTextBox.Text;
            }

            string password = string.Empty;
            if (PasswordTextBox.Text != null)
            {
                password = PasswordTextBox.Text;
            }

            RegisterViewModel registerViewModel = new RegisterViewModel();
            RegisterResult registerResult;
            try
            {
                registerResult = registerViewModel.RegisterUser(email, username, password);
            }
            catch (DataOperationException dataOperationException)
            {
                MessageTextBlock.Text = dataOperationException.Message;
                MessageTextBlock.Foreground = ErrorBrush;
                MessageTextBlock.IsVisible = true;
                return;
            }

            if (registerResult == RegisterResult.Success)
            {
                NavigateToLogin(Strings.RegisterSuccess);
                return;
            }

            MessageTextBlock.Text = GetErrorMessage(registerResult);
            MessageTextBlock.Foreground = ErrorBrush;
            MessageTextBlock.IsVisible = true;
        }

        private static string GetErrorMessage(RegisterResult registrationResult)
        {
            switch (registrationResult)
            {
                case RegisterResult.EmptyFields:
                    return Strings.RegisterErrorEmptyFields;
                case RegisterResult.InvalidEmail:
                    return Strings.RegisterErrorInvalidEmail;
                case RegisterResult.InvalidUsername:
                    return Strings.RegisterErrorInvalidUsername;
                case RegisterResult.WeakPassword:
                    return Strings.RegisterErrorWeakPassword;
                case RegisterResult.UsernameExists:
                    return Strings.RegisterErrorUserExists;
                case RegisterResult.EmailExists:
                    return Strings.RegisterErrorEmailExists;
                default:
                    return string.Empty;
            }
        }

        private void BackClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            NavigateToLogin(null);
        }

        private void NavigateToLogin(string? successMessage)
        {
            if (Application.Current != null &&
                Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                LoginWindow loginWindow;
                if (successMessage == null)
                {
                    loginWindow = new LoginWindow();
                }
                else
                {
                    loginWindow = new LoginWindow(successMessage);
                }

                desktopLifetime.MainWindow = loginWindow;
                loginWindow.Show();
                this.Close();
            }
        }
    }
}