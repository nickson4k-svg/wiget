using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CalWidget.Helpers
{
    public static class WindowGlassHelper
    {
        #region Native Win32 APIs

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        private const int DWMWCP_DONOTROUND = 1;
        private const int DWMWCP_ROUND = 2;

        private const int DWMSBT_AUTO = 0;
        private const int DWMSBT_NONE = 1;
        private const int DWMSBT_MAINWINDOW = 2; // Mica
        private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
        private const int DWMSBT_TABBEDWINDOW = 4; // Tabbed / Mica Alt

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_INVALID_STATE = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public uint GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private const int WCA_ACCENT_POLICY = 19;

        #endregion

        /// <summary>
        /// Застосовує розмиття матового скла (Windows 11 Acrylic або Windows 10 BlurBehind) до вікна WPF
        /// </summary>
        public static void ApplyFrostedGlass(Window window, bool isDarkTheme)
        {
            try
            {
                var helper = new WindowInteropHelper(window);
                IntPtr hwnd = helper.EnsureHandle();
                if (hwnd == IntPtr.Zero) return;

                int buildNumber = Environment.OSVersion.Version.Build;

                // Windows 11 (Build >= 22000): Встановлюємо лише тему DWM (Dark / Light)
                // НЕ викликаємо DWMWA_SYSTEMBACKDROP_TYPE та SetWindowCompositionAttribute,
                // тому що DWM малює глухий непрозорий прямокутник 90° на весь HWND позаду заокругленого Border!
                if (buildNumber >= 22000)
                {
                    int darkMode = isDarkTheme ? 1 : 0;
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WindowGlassHelper error: {ex.Message}");
            }
        }
    }
}
