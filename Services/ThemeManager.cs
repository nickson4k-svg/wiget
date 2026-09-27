using System;
using System.Linq;
using System.Windows;

namespace CalWidget.Services
{
    public static class ThemeManager
    {
        public static string CurrentTheme { get; private set; } = "Dark";
        public static event Action<string>? ThemeChanged;

        private static bool _isInitialized = false;

        public static void ApplyTheme(string theme)
        {
            string targetTheme = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";

            if (_isInitialized && string.Equals(CurrentTheme, targetTheme, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!_isInitialized && targetTheme == "Dark")
            {
                // App.xaml вже містить DarkTheme.xaml у MergedDictionaries
                _isInitialized = true;
                CurrentTheme = "Dark";
                ThemeChanged?.Invoke(CurrentTheme);
                return;
            }

            _isInitialized = true;
            if (targetTheme == "Light")
            {
                SetThemeResource("Themes/LightTheme.xaml");
                CurrentTheme = "Light";
            }
            else
            {
                SetThemeResource("Themes/DarkTheme.xaml");
                CurrentTheme = "Dark";
            }

            ThemeChanged?.Invoke(CurrentTheme);
        }

        public static void ToggleTheme()
        {
            ApplyTheme(CurrentTheme == "Dark" ? "Light" : "Dark");
        }

        private static void SetThemeResource(string relativeUri)
        {
            try
            {
                var dict = new ResourceDictionary
                {
                    Source = new Uri(relativeUri, UriKind.Relative)
                };

                // Замінюємо попередній словник теми або додаємо його
                var appResources = Application.Current.Resources;
                var existing = appResources.MergedDictionaries.FirstOrDefault(d => 
                    d.Source != null && (d.Source.OriginalString.Contains("DarkTheme.xaml") || d.Source.OriginalString.Contains("LightTheme.xaml")));

                if (existing != null)
                {
                    appResources.MergedDictionaries.Remove(existing);
                }

                appResources.MergedDictionaries.Add(dict);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка завантаження теми {relativeUri}: {ex.Message}");
            }
        }
    }
}
