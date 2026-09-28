using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace CalWidget.Helpers
{
    /// <summary>
    /// Сервіс прямого доступу до шпалер робочого столу Windows.
    /// Забезпечує створення повнорозмірної текстури для апаратного GPU-семплінгу (144+ FPS без GDI BitBlt затримок).
    /// </summary>
    public static class WallpaperHelper
    {
        private const int SPI_GETDESKWALLPAPER = 0x0073;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        /// <summary>
        /// Повертає абсолютний шлях до файлу активних шпалер робочого столу Windows.
        /// </summary>
        public static string? GetWallpaperFilePath()
        {
            try
            {
                // 1. Windows TranscodedWallpaper (актуальний кеш системи з оригінальним або обробленим зображенням)
                string transcoded = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Themes\TranscodedWallpaper");
                if (File.Exists(transcoded))
                {
                    return transcoded;
                }

                // 2. Реєстр Windows: HKCU\Control Panel\Desktop\Wallpaper
                string? regPath = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "Wallpaper", null) as string;
                if (!string.IsNullOrEmpty(regPath) && File.Exists(regPath))
                {
                    return regPath;
                }

                // 3. Win32 SystemParametersInfo
                string buffer = new string('\0', 260);
                if (SystemParametersInfo(SPI_GETDESKWALLPAPER, buffer.Length, buffer, 0) != 0)
                {
                    string path = buffer.TrimEnd('\0');
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        return path;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetWallpaperFilePath error: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Завантажує та масштабує шпалери під розмір віртуального екрана (BGRA32 піксельний масив).
        /// </summary>
        public static byte[]? LoadWallpaperPixels(int targetWidth, int targetHeight, out int outW, out int outH)
        {
            outW = targetWidth;
            outH = targetHeight;

            string? path = GetWallpaperFilePath();
            if (path == null) return null;

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0) return null;

                BitmapSource frame = decoder.Frames[0];
                int srcW = frame.PixelWidth;
                int srcH = frame.PixelHeight;

                if (srcW <= 0 || srcH <= 0) return null;

                // Масштабування за принципом UniformToFill (заповнює всю ширину/висоту зі збереженням пропорцій)
                double scaleX = (double)targetWidth / srcW;
                double scaleY = (double)targetHeight / srcH;
                double scale = Math.Max(scaleX, scaleY);

                int scaledW = Math.Max(targetWidth, (int)Math.Ceiling(srcW * scale));
                int scaledH = Math.Max(targetHeight, (int)Math.Ceiling(srcH * scale));

                int cropX = Math.Max(0, (scaledW - targetWidth) / 2);
                int cropY = Math.Max(0, (scaledH - targetHeight) / 2);

                BitmapSource scaledSource;
                if (Math.Abs(scale - 1.0) > 0.001)
                {
                    var scaleTransform = new ScaleTransform(scale, scale);
                    scaledSource = new TransformedBitmap(frame, scaleTransform);
                }
                else
                {
                    scaledSource = frame;
                }

                // Вирізаємо точно прямокутник targetWidth × targetHeight
                var cropped = new CroppedBitmap(scaledSource, new Int32Rect(cropX, cropY, targetWidth, targetHeight));
                var formatted = new FormatConvertedBitmap(cropped, PixelFormats.Bgra32, null, 0);

                byte[] buffer = new byte[targetWidth * targetHeight * 4];
                formatted.CopyPixels(buffer, targetWidth * 4, 0);
                return buffer;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadWallpaperPixels error: {ex.Message}");
                return null;
            }
        }
    }
}
