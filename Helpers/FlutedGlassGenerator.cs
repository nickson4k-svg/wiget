using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CalWidget.Helpers
{
    public class FlutedGlassOptions
    {
        // Головний перемикач ребристого скла (Fluted Glass vs Clean Mica Fluent)
        public bool EnableFlutedGlass { get; set; } = true;

        // Прозорість скла від 0.10 (10%) до 0.80 (80%)
        public double GlassOpacity { get; set; } = 0.60;

        // size: 0.61 -> ширина вертикальної смуги (8px .. 32px)
        public int StripeWidth { get; set; } = 12;

        // distortionShape: cascade, distortion: 0.75 -> амплітуда каскадного хвильового заломлення
        public double Distortion { get; set; } = 0.75;

        // shadows: 0.40 -> затемнення канавок між смугами (pow(0.40, 2) = 0.16)
        public double Shadows { get; set; } = 0.40;

        // blur: 0.07 -> вертикальне розмиття вздовж рифлення (0px .. 15px)
        public int BlurRadius { get; set; } = 3;

        // edges: 0.32 -> фаска та внутрішнє заломлення по краях
        public double Edges { get; set; } = 0.32;

        // Тема оформлення
        public bool IsDarkTheme { get; set; } = false;
    }

    /// <summary>
    /// Оптичний генератор Fluted Glass (Paper Design)
    /// Підтримує прямий in-place рендеринг у поновлюваний WriteableBitmap для досягнення 60 FPS без GC-пауз та смикань.
    /// </summary>
    public static class FlutedGlassGenerator
    {
        [ThreadStatic]
        private static byte[]? s_desktopBuffer;
        [ThreadStatic]
        private static byte[]? s_displacedBuffer;

        /// <summary>
        /// Малює каскадне скло безпосередньо в існуючий WriteableBitmap (In-Place, 0 GC allocations)
        /// </summary>
        public static void RenderCascadeGlassIntoBitmap(WriteableBitmap wb, int screenX, int screenY, int width, int height, FlutedGlassOptions options)
        {
            if (wb == null || width <= 0 || height <= 0) return;

            int bufferSize = width * height * 4;
            if (s_desktopBuffer == null || s_desktopBuffer.Length < bufferSize)
            {
                s_desktopBuffer = new byte[bufferSize];
            }
            if (s_displacedBuffer == null || s_displacedBuffer.Length < bufferSize)
            {
                s_displacedBuffer = new byte[bufferSize];
            }

            bool captured = CaptureScreenRegionToBuffer(screenX, screenY, width, height, s_desktopBuffer);
            if (!captured)
            {
                RenderFallbackIntoBitmap(wb, width, height, options);
                return;
            }

            ProcessCascadeGlassIntoBitmap(wb, s_desktopBuffer, s_displacedBuffer, width, height, options);
        }

        public static WriteableBitmap CreateCascadeGlassBitmap(int screenX, int screenY, int width, int height, FlutedGlassOptions options)
        {
            if (width <= 0) width = 300;
            if (height <= 0) height = 490;

            var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            RenderCascadeGlassIntoBitmap(wb, screenX, screenY, width, height, options);
            return wb;
        }

        private static bool CaptureScreenRegionToBuffer(int x, int y, int width, int height, byte[] buffer)
        {
            IntPtr hdcScreen = IntPtr.Zero;
            IntPtr hdcMem = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOldBmp = IntPtr.Zero;

            try
            {
                hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
                if (hdcScreen == IntPtr.Zero) return false;

                hdcMem = NativeMethods.CreateCompatibleDC(hdcScreen);
                if (hdcMem == IntPtr.Zero) return false;

                var bmi = new NativeMethods.BITMAPINFO
                {
                    bmiHeader = new NativeMethods.BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf(typeof(NativeMethods.BITMAPINFOHEADER)),
                        biWidth = width,
                        biHeight = -height, // Top-down DIB
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = 0 // BI_RGB
                    }
                };

                hBitmap = NativeMethods.CreateDIBSection(hdcMem, ref bmi, NativeMethods.DIB_RGB_COLORS, out IntPtr ppvBits, IntPtr.Zero, 0);
                if (hBitmap == IntPtr.Zero || ppvBits == IntPtr.Zero) return false;

                hOldBmp = NativeMethods.SelectObject(hdcMem, hBitmap);

                bool success = NativeMethods.BitBlt(
                    hdcMem, 0, 0, width, height,
                    hdcScreen, x, y,
                    NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);

                if (!success) return false;

                int bufferSize = width * height * 4;
                Marshal.Copy(ppvBits, buffer, 0, bufferSize);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (hdcMem != IntPtr.Zero && hOldBmp != IntPtr.Zero)
                    NativeMethods.SelectObject(hdcMem, hOldBmp);
                if (hBitmap != IntPtr.Zero)
                    NativeMethods.DeleteObject(hBitmap);
                if (hdcMem != IntPtr.Zero)
                    NativeMethods.DeleteDC(hdcMem);
                if (hdcScreen != IntPtr.Zero)
                    NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);
            }
        }

        private static void ProcessCascadeGlassIntoBitmap(WriteableBitmap wb, byte[] sourcePixels, byte[] displacedPixels, int width, int height, FlutedGlassOptions options)
        {
            int stripeW = Math.Max(4, options.StripeWidth);
            double distortionStrength = options.Distortion; // 0.75
            double shadowBase = Math.Pow(options.Shadows, 2.0); // pow(0.40, 2) = 0.16
            int blurR = Math.Max(0, options.BlurRadius);

            int[] lutSrcX = new int[width];
            float[] lutShadow = new float[width];

            double twoPi = 2.0 * Math.PI;
            double halfPi = 0.5 * Math.PI;

            for (int px = 0; px < width; px++)
            {
                if (!options.EnableFlutedGlass)
                {
                    lutShadow[px] = 0.0f;
                    lutSrcX[px] = px;
                }
                else
                {
                    double u = (double)(px % stripeW) / stripeW;
                    double sinVal = Math.Sin((u + 0.25) * twoPi);
                    double distortion = sinVal * 0.5 * 3.0 * distortionStrength;

                    double clampedSin = Math.Clamp(sinVal, -1.0, 1.0);
                    double shadowVal = 0.5 + (0.5 * (Math.Asin(clampedSin) / halfPi));
                    shadowVal = Math.Clamp(shadowVal * shadowBase, 0.0, 1.0);
                    lutShadow[px] = (float)shadowVal;

                    int displacedX = (int)Math.Round(px + (distortion * stripeW));
                    lutSrcX[px] = Math.Clamp(displacedX, 0, width - 1);
                }
            }

            unsafe
            {
                fixed (byte* pSrc = sourcePixels, pDisp = displacedPixels)
                {
                    int stride = width * 4;

                    for (int y = 0; y < height; y++)
                    {
                        byte* rowSrc = pSrc + (y * stride);
                        byte* rowDisp = pDisp + (y * stride);

                        for (int x = 0; x < width; x++)
                        {
                            int sx = lutSrcX[x];
                            int srcIdx = sx * 4;
                            int dstIdx = x * 4;

                            rowDisp[dstIdx + 0] = rowSrc[srcIdx + 0]; // B
                            rowDisp[dstIdx + 1] = rowSrc[srcIdx + 1]; // G
                            rowDisp[dstIdx + 2] = rowSrc[srcIdx + 2]; // R
                            rowDisp[dstIdx + 3] = 255;                // A
                        }
                    }
                }
            }

            wb.Lock();
            try
            {
                unsafe
                {
                    byte* pDst = (byte*)wb.BackBuffer;
                    int stride = wb.BackBufferStride;

                    fixed (byte* pDisp = displacedPixels)
                    {
                        float tintR = options.IsDarkTheme ? 20f : 250f;
                        float tintG = options.IsDarkTheme ? 22f : 252f;
                        float tintB = options.IsDarkTheme ? 26f : 255f;
                        float tintAlpha = (float)Math.Clamp(options.GlassOpacity, 0.05, 0.90);

                        if (blurR <= 0)
                        {
                            for (int y = 0; y < height; y++)
                            {
                                byte* rowDisp = pDisp + (y * stride);
                                byte* outRow = pDst + (y * stride);

                                for (int x = 0; x < width; x++)
                                {
                                    float shadow = lutShadow[x];
                                    float lightMul = (1.0f - shadow);

                                    int idx = x * 4;
                                    float b = rowDisp[idx + 0] * lightMul;
                                    float g = rowDisp[idx + 1] * lightMul;
                                    float r = rowDisp[idx + 2] * lightMul;

                                    float finalR = (r * (1.0f - tintAlpha)) + (tintR * tintAlpha);
                                    float finalG = (g * (1.0f - tintAlpha)) + (tintG * tintAlpha);
                                    float finalB = (b * (1.0f - tintAlpha)) + (tintB * tintAlpha);

                                    outRow[idx + 0] = (byte)Math.Clamp((int)finalB, 0, 255);
                                    outRow[idx + 1] = (byte)Math.Clamp((int)finalG, 0, 255);
                                    outRow[idx + 2] = (byte)Math.Clamp((int)finalR, 0, 255);
                                    outRow[idx + 3] = 255;
                                }
                            }
                        }
                        else
                        {
                            for (int x = 0; x < width; x++)
                            {
                                float shadow = lutShadow[x];
                                float lightMul = (1.0f - shadow);

                                long sumB = 0, sumG = 0, sumR = 0;
                                int count = 0;

                                int startLimit = Math.Min(blurR, height - 1);
                                for (int y = 0; y <= startLimit; y++)
                                {
                                    int idx = (y * width + x) * 4;
                                    sumB += pDisp[idx + 0];
                                    sumG += pDisp[idx + 1];
                                    sumR += pDisp[idx + 2];
                                    count++;
                                }

                                for (int y = 0; y < height; y++)
                                {
                                    int addY = y + blurR;
                                    if (addY < height && addY > startLimit)
                                    {
                                        int addIdx = (addY * width + x) * 4;
                                        sumB += pDisp[addIdx + 0];
                                        sumG += pDisp[addIdx + 1];
                                        sumR += pDisp[addIdx + 2];
                                        count++;
                                    }

                                    int remY = y - blurR - 1;
                                    if (remY >= 0)
                                    {
                                        int remIdx = (remY * width + x) * 4;
                                        sumB -= pDisp[remIdx + 0];
                                        sumG -= pDisp[remIdx + 1];
                                        sumR -= pDisp[remIdx + 2];
                                        count--;
                                    }

                                    float blurredB = ((float)sumB / count) * lightMul;
                                    float blurredG = ((float)sumG / count) * lightMul;
                                    float blurredR = ((float)sumR / count) * lightMul;

                                    float finalR = (blurredR * (1.0f - tintAlpha)) + (tintR * tintAlpha);
                                    float finalG = (blurredG * (1.0f - tintAlpha)) + (tintG * tintAlpha);
                                    float finalB = (blurredB * (1.0f - tintAlpha)) + (tintB * tintAlpha);

                                    byte* outRow = pDst + (y * stride);
                                    int outIdx = x * 4;

                                    outRow[outIdx + 0] = (byte)Math.Clamp((int)finalB, 0, 255);
                                    outRow[outIdx + 1] = (byte)Math.Clamp((int)finalG, 0, 255);
                                    outRow[outIdx + 2] = (byte)Math.Clamp((int)finalR, 0, 255);
                                    outRow[outIdx + 3] = 255;
                                }
                            }
                        }
                    }
                }

                wb.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                wb.Unlock();
            }
        }

        private static void RenderFallbackIntoBitmap(WriteableBitmap wb, int width, int height, FlutedGlassOptions options)
        {
            int stripeW = Math.Max(4, options.StripeWidth);
            int baseR = options.IsDarkTheme ? 22 : 248;
            int baseG = options.IsDarkTheme ? 24 : 250;
            int baseB = options.IsDarkTheme ? 28 : 254;

            wb.Lock();
            try
            {
                unsafe
                {
                    byte* ptr = (byte*)wb.BackBuffer;
                    int stride = wb.BackBufferStride;

                    for (int y = 0; y < height; y++)
                    {
                        byte* row = ptr + (y * stride);

                        for (int x = 0; x < width; x++)
                        {
                            double u = (double)(x % stripeW) / stripeW;
                            double sinVal = Math.Sin((u + 0.25) * 2.0 * Math.PI);
                            double shadow = (0.5 + (0.5 * (Math.Asin(Math.Clamp(sinVal, -1.0, 1.0)) / (0.5 * Math.PI)))) * 0.16;
                            double light = 1.0 - shadow;

                            int idx = x * 4;
                            row[idx + 0] = (byte)Math.Clamp((int)(baseB * light), 0, 255);
                            row[idx + 1] = (byte)Math.Clamp((int)(baseG * light), 0, 255);
                            row[idx + 2] = (byte)Math.Clamp((int)(baseR * light), 0, 255);
                            row[idx + 3] = 255;
                        }
                    }
                }

                wb.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                wb.Unlock();
            }
        }

        public static WriteableBitmap CreateFallbackGlassBitmap(int width, int height, FlutedGlassOptions options)
        {
            if (width <= 0) width = 300;
            if (height <= 0) height = 490;

            var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            RenderFallbackIntoBitmap(wb, width, height, options);
            return wb;
        }
    }
}
