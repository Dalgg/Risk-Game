using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using RiskGame.ViewModels;

namespace RiskGame
{
    /// <summary>
    /// Localizador de vistas de Avalonia que resuelve la ventana asociada a un modelo de vista
    /// mediante la convención de nombres <c>NombreDelViewModel</c> y <c>NombreDelView</c>.
    /// </summary>
    [RequiresUnreferencedCode(
        "Default implementation of ViewLocator involves reflection which may be trimmed away.",
        Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
    public class ViewLocator : IDataTemplate
    {
        /// <summary>
        /// Construye la vista que corresponde al modelo de vista recibido.
        /// </summary>
        /// <param name="param">Instancia del modelo de vista que se desea representar en pantalla.</param>
        /// <returns>
        /// El control asociado al modelo de vista, o un <see cref="TextBlock"/> informativo cuando
        /// no existe una vista que cumpla la convención de nombres.
        /// </returns>
        public Control? Build(object? param)
        {
            if (param is null)
            {
                return null;
            }

            string viewName = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
            Type? viewType = Type.GetType(viewName);

            if (viewType == null)
            {
                return new TextBlock { Text = "Not Found: " + viewName };
            }

            return (Control)Activator.CreateInstance(viewType)!;
        }

        /// <summary>
        /// Indica si el objeto evaluado puede ser representado por este localizador de vistas.
        /// </summary>
        /// <param name="data">Objeto consultado por el motor de plantillas de Avalonia.</param>
        /// <returns><c>true</c> si el objeto es un modelo de vista de esta aplicación.</returns>
        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}