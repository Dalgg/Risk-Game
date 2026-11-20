using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Resources;
using RiskGame.Services;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class RegisterWindow : Window
    {
        private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#FF8A8A"));
        private readonly RegisterViewModel registerViewModel = new RegisterViewModel();
        private bool hasClosed;

        public RegisterWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs eventArgs)
        {
            hasClosed = true;
            base.OnClosed(eventArgs);
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

            RegisterConfirmButton.IsEnabled = false;
            MessageTextBlock.IsVisible = false;
            Task.Run(() => SubmitRegistration(email, username, password));
        }

        private void SubmitRegistration(string email, string username, string password)
        {
            try
            {
                RegisterResult registerResult = registerViewModel.RegisterUser(email, username, password);
                Dispatcher.UIThread.Post(() => ApplyRegistrationResult(registerResult));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not begin email verification during registration.");
                Dispatcher.UIThread.Post(() => ShowRegistrationError(dataOperationException.Message));
            }
        }

        private void ApplyRegistrationResult(RegisterResult registerResult)
        {
            if (!hasClosed)
            {
                RegisterConfirmButton.IsEnabled = true;
                if (registerResult == RegisterResult.Success)
                {
                    RegistrationForm.IsVisible = false;
                    RegisterConfirmButton.IsVisible = false;
                    VerificationPanel.IsVisible = true;
                    PasswordTextBox.Text = string.Empty;
                    ShowVerificationMessage(Strings.VerificationSent, Brushes.White);
                    VerificationCodeTextBox.Focus();
                }
                else
                {
                    ShowRegistrationError(GetErrorMessage(registerResult));
                }
            }
        }

        private void ShowRegistrationError(string message)
        {
            if (!hasClosed)
            {
                RegisterConfirmButton.IsEnabled = true;
                ShowVerificationMessage(message, ErrorBrush);
            }
        }

        private void OnVerifyClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            string code = string.Empty;
            if (VerificationCodeTextBox.Text != null)
            {
                code = VerificationCodeTextBox.Text;
            }

            SetVerificationButtonsEnabled(false);
            Task.Run(() => SubmitVerification(code));
        }

        private void SubmitVerification(string code)
        {
            try
            {
                RegisterResult result = registerViewModel.ConfirmRegistration(code);
                Dispatcher.UIThread.Post(() => ApplyVerificationResult(result));
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not complete the email verification.");
                Dispatcher.UIThread.Post(() => ShowVerificationError(dataOperationException.Message));
            }
        }

        private void ApplyVerificationResult(RegisterResult result)
        {
            if (!hasClosed)
            {
                SetVerificationButtonsEnabled(true);
                if (result == RegisterResult.Success)
                {
                    NavigateToLogin(Strings.RegisterSuccess);
                }
                else
                {
                    ShowVerificationMessage(GetErrorMessage(result), ErrorBrush);
                }
            }
        }

        private void OnResendClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            SetVerificationButtonsEnabled(false);
            Task.Run(SubmitResendVerification);
        }

        private void SubmitResendVerification()
        {
            try
            {
                registerViewModel.ResendVerification();
                Dispatcher.UIThread.Post(ApplyResendSuccess);
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not resend the email verification.");
                Dispatcher.UIThread.Post(() => ShowVerificationError(dataOperationException.Message));
            }
        }

        private void ApplyResendSuccess()
        {
            if (!hasClosed)
            {
                SetVerificationButtonsEnabled(true);
                ShowVerificationMessage(Strings.VerificationSent, Brushes.White);
            }
        }

        private void ShowVerificationError(string message)
        {
            if (!hasClosed)
            {
                SetVerificationButtonsEnabled(true);
                ShowVerificationMessage(message, ErrorBrush);
            }
        }

        private void SetVerificationButtonsEnabled(bool isEnabled)
        {
            VerifyConfirmButton.IsEnabled = isEnabled;
            ResendCodeButton.IsEnabled = isEnabled;
        }

        private void ShowVerificationMessage(string message, IBrush foregroundBrush)
        {
            MessageTextBlock.Text = message;
            MessageTextBlock.Foreground = foregroundBrush;
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
            if (Application.Current != null
                && Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
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
                Close();
            }
        }
    }
}
