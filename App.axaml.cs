using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia;
using RiskGame.ViewModels;
using RiskGame.Views;

namespace RiskGame
{

    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            RiskGame.Resources.Strings.Culture = new System.Globalization.CultureInfo("en-US");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel(),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
