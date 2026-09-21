using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Risk_Game.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
    }

    private void OnRegisterClick(object? sender, RoutedEventArgs e)
    {
        var registerWindow = new RegisterWindow();
        registerWindow.Show();
    }
}
