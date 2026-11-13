using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using RiskGame.Models;
using RiskGame.Resources;
using RiskGame.Exceptions;
using RiskGame.ViewModels;
using RiskGame.Services;

namespace RiskGame.Views
{
    public partial class RegisterWindow : Window
    {
        private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#FF8A8A"));
        private readonly RegisterViewModel registerViewModel = new RegisterViewModel();

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

            RegisterResult? registerResult = null;
            try
            {
                registerResult = registerViewModel.RegisterUser(email, username, password);
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not begin email verification during registration.");
                MessageTextBlock.Text = dataOperationException.Message;
                MessageTextBlock.Foreground = ErrorBrush;
                MessageTextBlock.IsVisible = true;
            }

            if (registerResult.HasValue && registerResult.Value == RegisterResult.Success)
            {
                RegistrationForm.IsVisible = false;
                RegisterConfirmButton.IsVisible = false;
                VerificationPanel.IsVisible = true;
                PasswordTextBox.Text = string.Empty;
                MessageTextBlock.Text = Strings.VerificationSent;
                MessageTextBlock.IsVisible = true;
                VerificationCodeTextBox.Focus();
            }
            else if (registerResult.HasValue)
            {
                MessageTextBlock.Text = GetErrorMessage(registerResult.Value);
                MessageTextBlock.Foreground = ErrorBrush;
                MessageTextBlock.IsVisible = true;
            }
        }

        private void OnVerifyClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            string code = string.Empty;
            if (VerificationCodeTextBox.Text != null)
            {
                code = VerificationCodeTextBox.Text;
            }
            try
            {
                RegisterResult result = registerViewModel.ConfirmRegistration(code);
                if (result == RegisterResult.Success)
                {
                    NavigateToLogin(Strings.RegisterSuccess);
                }
                else
                {
                    ShowVerificationMessage(GetErrorMessage(result));
                }
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not complete the email verification.");
                ShowVerificationMessage(dataOperationException.Message);
            }
        }

        private void OnResendClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            try
            {
                registerViewModel.ResendVerification();
                ShowVerificationMessage(Strings.VerificationSent);
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not resend the email verification.");
                ShowVerificationMessage(dataOperationException.Message);
            }
        }

        private void ShowVerificationMessage(string message)
        {
            MessageTextBlock.Text = message;
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
