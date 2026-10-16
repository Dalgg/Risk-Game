using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using RiskGame.ViewModels;

namespace RiskGame
{
    [RequiresUnreferencedCode(
        "Default implementation of ViewLocator involves reflection which may be trimmed away.",
        Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
    public class ViewLocator : IDataTemplate
    {
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

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}