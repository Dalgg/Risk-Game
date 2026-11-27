using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class ForgotPasswordWindow : Window
    {
        private static readonly IBrush ErrorBrush = new ImmutableSolidColorBrush(Color.Parse("#FF8A8A"));
        private static readonly IBrush SuccessBrush = new ImmutableSolidColorBrush(Color.Parse("#8CE99A"));
        private readonly ForgotPasswordViewModel viewModel = new ForgotPasswordViewModel();
        private bool hasClosed;

        public ForgotPasswordWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs eventArgs)
        {
            hasClosed = true;
            base.OnClosed(eventArgs);
        }

        private void OnSendCodeClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            string email = ReadText(EmailTextBox);
            if (!ForgotPasswordViewModel.IsValidEmail(email))
            {
                ShowMessage(Strings.ForgotPasswordEmailInvalid, ErrorBrush);
            }
            else
            {
                SendCodeButton.IsEnabled = false;
                MessageTextBlock.IsVisible = false;
                Task.Run(() => SubmitResetRequest(email));
            }
        }

        private void SubmitResetRequest(string email)
        {
            try
            {
                viewModel.RequestPasswordReset(email);
                Dispatcher.UIThread.Post(ApplyResetRequestSuccess);
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not request password reset code.");
                Dispatcher.UIThread.Post(() => ShowRequestError(dataOperationException.Message));
            }
        }

        private void ApplyResetRequestSuccess()
        {
            if (!hasClosed)
            {
                SendCodeButton.IsEnabled = true;
                EmailPanel.IsVisible = false;
                ResetPanel.IsVisible = true;
                ShowMessage(Strings.ForgotPasswordCodeSent, SuccessBrush);
                ResetCodeTextBox.Focus();
            }
        }

        private void ShowRequestError(string message)
        {
            if (!hasClosed)
            {
                SendCodeButton.IsEnabled = true;
                ShowMessage(message, ErrorBrush);
            }
        }

        private void OnConfirmResetClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            string code = ReadText(ResetCodeTextBox);
            string newPassword = ReadText(NewPasswordTextBox);
            string confirmPassword = ReadText(ConfirmPasswordTextBox);
            string validationError = ForgotPasswordViewModel.ValidateResetInputs(code, newPassword, confirmPassword);
            if (!string.IsNullOrEmpty(validationError))
            {
                ShowMessage(validationError, ErrorBrush);
            }
            else
            {
                SetResetButtonsEnabled(false);
                Task.Run(() => SubmitConfirmReset(code, newPassword, confirmPassword));
            }
        }

        private void SubmitConfirmReset(string code, string newPassword, string confirmPassword)
        {
            try
            {
                viewModel.ConfirmPasswordReset(code, newPassword, confirmPassword);
                Dispatcher.UIThread.Post(ApplyConfirmResetSuccess);
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not confirm password reset.");
                Dispatcher.UIThread.Post(() => ShowResetError(dataOperationException.Message));
            }
        }

        private void ApplyConfirmResetSuccess()
        {
            if (!hasClosed)
            {
                NavigateToLogin(Strings.ForgotPasswordSuccess);
            }
        }

        private void OnResendClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            SetResetButtonsEnabled(false);
            Task.Run(SubmitResendReset);
        }

        private void SubmitResendReset()
        {
            try
            {
                viewModel.ResendPasswordReset();
                Dispatcher.UIThread.Post(ApplyResendSuccess);
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Could not resend password reset code.");
                Dispatcher.UIThread.Post(() => ShowResetError(dataOperationException.Message));
            }
        }

        private void ApplyResendSuccess()
        {
            if (!hasClosed)
            {
                SetResetButtonsEnabled(true);
                ShowMessage(Strings.ForgotPasswordCodeSent, SuccessBrush);
            }
        }

        private void ShowResetError(string message)
        {
            if (!hasClosed)
            {
                SetResetButtonsEnabled(true);
                ShowMessage(message, ErrorBrush);
            }
        }

        private void SetResetButtonsEnabled(bool isEnabled)
        {
            ConfirmResetButton.IsEnabled = isEnabled;
            ResendCodeButton.IsEnabled = isEnabled;
        }

        private void ShowMessage(string message, IBrush foregroundBrush)
        {
            MessageTextBlock.Text = message;
            MessageTextBlock.Foreground = foregroundBrush;
            MessageTextBlock.IsVisible = true;
        }

        private static string ReadText(TextBox textBox)
        {
            if (textBox.Text != null)
            {
                return textBox.Text;
            }
            return string.Empty;
        }

        private void BackClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            NavigateToLogin(null);
        }

        private void NavigateToLogin(string? successMessage)
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

            if (Application.Current != null
                && Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                desktopLifetime.MainWindow = loginWindow;
            }

            loginWindow.Show();
            Close();
        }
    }
}
