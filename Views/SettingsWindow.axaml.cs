using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.Resources;

namespace RiskGame.Views
{
    /// <summary>
    /// Ventana de configuración de la aplicación. Permite cambiar el idioma de la interfaz y
    /// refleja el idioma vigente al abrirse.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        /// <summary>
        /// Identificador de la opción que corresponde al idioma español.
        /// </summary>
        private const int SpanishOptionIndex = 0;

        /// <summary>
        /// Identificador de la opción que corresponde al idioma inglés.
        /// </summary>
        private const int EnglishOptionIndex = 1;

        /// <summary>
        /// Crea la ventana de configuración y preselecciona el idioma activo.
        /// </summary>
        public SettingsWindow()
        {
            InitializeComponent();

            bool isEnglishActive = Strings.Culture != null && Strings.Culture.Name == "en-US";
            LanguageComboBox.SelectedIndex = isEnglishActive ? EnglishOptionIndex : SpanishOptionIndex;
        }

        /// <summary>
        /// Aplica el idioma seleccionado por el jugador a los recursos de la interfaz.
        /// </summary>
        /// <param name="sender">ComboBox que originó el evento de selección.</param>
        /// <param name="e">Datos asociados al evento de cambio de selección.</param>
        private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox)
            {
                return;
            }

            switch (comboBox.SelectedIndex)
            {
                case SpanishOptionIndex:
                    Strings.Culture = new CultureInfo("es");
                    break;

                case EnglishOptionIndex:
                    Strings.Culture = new CultureInfo("en-US");
                    break;
            }
        }

        /// <summary>
        /// Regresa a la ventana principal.
        /// </summary>
        /// <param name="sender">Elemento que originó el evento.</param>
        /// <param name="e">Datos asociados al evento de interacción.</param>
        private void OnBackClick(object? sender, RoutedEventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                MainWindow mainWindow = new MainWindow();
                desktopLifetime.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
        }
    }
}