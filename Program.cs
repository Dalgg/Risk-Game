using System;
using Avalonia;

namespace RiskGame
{
    /// <summary>
    /// Punto de entrada del proceso. Configura el ciclo de vida de la aplicación Avalonia.
    /// </summary>
    internal sealed class Program
    {
        /// <summary>
        /// Inicializa el entorno gráfico y arranca el ciclo de vida de escritorio clásico.
        /// </summary>
        /// <param name="args">Argumentos de línea de comandos propagados por el sistema operativo.</param>
        [STAThread]
        public static void Main(string[] args)
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        /// <summary>
        /// Construye el <see cref="AppBuilder"/> con los proveedores de plataforma y de fuentes
        /// requeridos por la aplicación. También lo consume el diseñador visual.
        /// </summary>
        /// <returns>El constructor de la aplicación ya configurado para la plataforma actual.</returns>
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
#if DEBUG
                .WithDeveloperTools()
#endif
                .WithInterFont()
                .LogToTrace();
        }
    }
}