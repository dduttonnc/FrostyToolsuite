using System;
using System.Windows;

namespace Frosty.Controls
{
    public static class FrostyThemeManager
    {
        public const string DefaultTheme = "Dark";
        public static readonly string[] ThemeNames = { "Dark", "Light", "Frosty", "DarkFrosty", "Original" };
        public static readonly string[] ThemeDisplayNames = { "Dark", "Light", "Frosty", "Dark Frosty", "Original" };

        public static string CurrentTheme { get; private set; } = DefaultTheme;

        public static event EventHandler ThemeChanged;

        private static ResourceDictionary appliedTheme;

        // Added last so its keys win over the defaults merged into Generic.xaml; DynamicResource users update live.
        public static void Apply(string themeName)
        {
            Application app = Application.Current;
            if (app == null)
                return;

            if (Array.IndexOf(ThemeNames, themeName) < 0)
                themeName = DefaultTheme;

            if (appliedTheme != null && themeName == CurrentTheme)
                return;

            ResourceDictionary theme = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/FrostyControls;component/Themes/Colors/" + themeName + ".xaml", UriKind.Absolute)
            };

            if (appliedTheme != null)
                app.Resources.MergedDictionaries.Remove(appliedTheme);
            app.Resources.MergedDictionaries.Add(theme);

            appliedTheme = theme;
            CurrentTheme = themeName;
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
