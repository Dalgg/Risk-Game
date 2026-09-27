using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RiskGame.Views
{

    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void OnRegisterClick(object? sender, RoutedEventArgs e)
        {
            RegisterWindow registerWindow = new RegisterWindow();
            registerWindow.Show();
        }
    }

}
