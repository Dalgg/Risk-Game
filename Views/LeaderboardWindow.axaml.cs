using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.ViewModels;

namespace RiskGame.Views
{

public partial class LeaderboardWindow : Window
{
    private readonly LeaderboardViewModel _viewModel;

    public LeaderboardWindow()
    {
        InitializeComponent();
        _viewModel = new LeaderboardViewModel();
        DataContext = _viewModel;
    }

    private void OnSortClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ToggleSortDirection();
    }

    private void OnGlobalClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.LoadGlobalScores();
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
}
}
