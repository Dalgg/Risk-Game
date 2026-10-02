using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Globalization;
using RiskGame.ViewModels;
using RiskGame.Views;

namespace RiskGame
{
    /// <summary>
    /// Punto de entrada de la aplicación Avalonia. Carga los recursos declarados en XAML y
    /// configura la ventana principal junto con el idioma inicial de la interfaz.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Carga los recursos XAML definidos en <c>App.axaml</c>.
        /// </summary>
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Fuerza el idioma de la interfaz y crea la ventana principal una vez que el framework
        /// de Avalonia ha finalizado su inicialización.
        /// </summary>
        public override void OnFrameworkInitializationCompleted()
        {
            RiskGame.Resources.Strings.Culture = new CultureInfo("en-US");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                desktopLifetime.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel(),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}