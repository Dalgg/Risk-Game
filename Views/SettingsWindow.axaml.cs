using Avalonia.Controls;
using System.Globalization;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using RiskGame.Resources;

namespace RiskGame.Views
{

    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            if (Strings.Culture != null && Strings.Culture.Name == "en-US")
            {
                LanguageComboBox.SelectedIndex = 1;
            }
            else
            {
                LanguageComboBox.SelectedIndex = 0;
            }
        }

        private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                switch (comboBox.SelectedIndex)
                {
                    case 0:
                        Strings.Culture = new CultureInfo("es");
                        break;
                    case 1:
                        Strings.Culture = new CultureInfo("en-US");
                        break;
                }
            }
        }

        private void OnBackClick(object? sender, RoutedEventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();
                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                this.Close(); 
            }
        }
    }
}