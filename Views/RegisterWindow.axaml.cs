using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using RiskGame.Models;
using RiskGame.Resources;
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

        private void OnConfirmClick(object? sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text ?? string.Empty;
            string user = UsernameTextBox.Text ?? string.Empty;
            string password = PasswordTextBox.Text ?? string.Empty;

            RegisterViewModel viewModel = new RegisterViewModel();
            RegisterResult registerResult = viewModel.RegisterUser(email, user, password);

            if (registerResult == RegisterResult.Success)
            {
                NavigateToLogin(Strings.RegisterSuccess);
                return;
            }

            MessageTextBlock.Text = GetErrorMessage(registerResult);
            MessageTextBlock.Foreground = ErrorBrush;
            MessageTextBlock.IsVisible = true;
        }

        private static string GetErrorMessage(RegisterResult result)
        {
            return result switch
            {
                RegisterResult.EmptyFields => Strings.RegisterErrorEmptyFields,
                RegisterResult.InvalidEmail => Strings.RegisterErrorInvalidEmail,
                RegisterResult.InvalidUsername => Strings.RegisterErrorInvalidUsername,
                RegisterResult.WeakPassword => Strings.RegisterErrorWeakPassword,
                RegisterResult.UsernameExists => Strings.RegisterErrorUserExists,
                RegisterResult.EmailExists => Strings.RegisterErrorEmailExists,
                _ => string.Empty
            };
        }

        private void OnBackClick(object? sender, RoutedEventArgs e)
        {
            NavigateToLogin(null);
        }

        private void NavigateToLogin(string? successMessage)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                LoginWindow loginWindow = successMessage == null
                    ? new LoginWindow()
                    : new LoginWindow(successMessage);
                desktop.MainWindow = loginWindow;
                loginWindow.Show();
                this.Close();
            }
        }
    }
}