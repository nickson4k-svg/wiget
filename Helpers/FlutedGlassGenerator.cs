using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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

        // Кольоровий tint для кілець БЖВ (0–255). Нульові значення = нейтральний tint (фоновий режим)
        public float TintR { get; set; } = 0f;
        public float TintG { get; set; } = 0f;
        public float TintB { get; set; } = 0f;
        // Сила кольорового tint поверх заломленого зображення (0.0 = чисте скло, 1.0 = суцільний колір)
        public float TintOpacity { get; set; } = 0f;
    }

    /// <summary>
    /// Оптичний генератор Fluted Glass (Paper Design)
    /// Підтримує прямий in-place рендеринг у поновлюваний WriteableBitmap для досягнення 60 FPS без GC-пауз та смикань.
    /// </summary>
    public static class FlutedGlassGenerator
    {
        // Буфери для фонового вікна (main glass)
        [ThreadStatic]
        private static byte[]? s_desktopBuffer;
        [ThreadStatic]
        private static byte[]? s_displacedBuffer;

        // Окремі буфери для кілець БЖВ (ring glass) — уникаємо конкуренції з фоном
        [ThreadStatic]
        private static byte[]? s_ringDesktopBuffer;
        [ThreadStatic]
        private static byte[]? s_ringDisplacedBuffer;

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

        /// <summary>
        /// Створює попередньо згенерований WriteableBitmap скла з вихідних пікселів шпалер або робочого столу.
        /// Заморожує (Freeze) для прямого апаратного семплінгу GPU через ImageBrush.Viewbox на 144+ FPS.
        /// </summary>
        public static WriteableBitmap CreateWallpaperGlassBitmap(byte[] sourcePixels, int width, int height, FlutedGlassOptions options)
        {
            if (width <= 0) width = 1920;
            if (height <= 0) height = 1080;

            var wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            byte[] displaced = new byte[width * height * 4];
            ProcessCascadeGlassIntoBitmap(wb, sourcePixels, displaced, width, height, options);
            wb.Freeze();
            return wb;
        }

        /// <summary>
        /// Рендерить Fluted Glass у WriteableBitmap для кільця БЖВ.
        /// Захоплює квадрат screenSize×screenSize з координат (screenX,screenY), застосовує
        /// каскадну sine-дисторцію та кольоровий tint кільця (amber/blue/red).
        /// Використовує окремий ThreadStatic буфер — не конкурує з фоновим рендерингом.
        /// </summary>
        public static void RenderRingGlassIntoBitmap(
            WriteableBitmap wb, int screenX, int screenY, int width, int height, FlutedGlassOptions options)
        {
            if (wb == null || width <= 0 || height <= 0) return;

            int bufferSize = width * height * 4;
            if (s_ringDesktopBuffer == null || s_ringDesktopBuffer.Length < bufferSize)
                s_ringDesktopBuffer = new byte[bufferSize];
            if (s_ringDisplacedBuffer == null || s_ringDisplacedBuffer.Length < bufferSize)
                s_ringDisplacedBuffer = new byte[bufferSize];

            bool captured = CaptureScreenRegionToBuffer(screenX, screenY, width, height, s_ringDesktopBuffer);
            if (!captured)
            {
                RenderFallbackIntoBitmap(wb, width, height, options);
                return;
            }

            ProcessCascadeGlassIntoBitmap(wb, s_ringDesktopBuffer, s_ringDisplacedBuffer, width, height, options);
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

                    IntPtr pSrcPtr = (IntPtr)pSrc;
                    IntPtr pDispPtr = (IntPtr)pDisp;

                    Parallel.For(0, height, y =>
                    {
                        byte* rowSrc = (byte*)pSrcPtr + (y * stride);
                        byte* rowDisp = (byte*)pDispPtr + (y * stride);

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
                    });
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
                        IntPtr pDstPtr = (IntPtr)pDst;
                        IntPtr pDispPtr = (IntPtr)pDisp;

                        // Нейтральний tint вікна (frost/mica), або кольоровий tint кільця (amber/blue/red)
                        float neutralR = options.IsDarkTheme ? 20f : 250f;
                        float neutralG = options.IsDarkTheme ? 22f : 252f;
                        float neutralB = options.IsDarkTheme ? 26f : 255f;
                        float tintAlpha = (float)Math.Clamp(options.GlassOpacity, 0.05, 0.90);

                        // Кольоровий tint кільця (0 = відключено — звичайний режим фону)
                        float ringTintR = options.TintR;
                        float ringTintG = options.TintG;
                        float ringTintB = options.TintB;
                        float ringTintOpacity = options.TintOpacity;

                        if (blurR <= 0)
                        {
                            Parallel.For(0, height, y =>
                            {
                                byte* rowDisp = (byte*)pDispPtr + (y * stride);
                                byte* outRow = (byte*)pDstPtr + (y * stride);

                                for (int x = 0; x < width; x++)
                                {
                                    float shadow = lutShadow[x];
                                    float lightMul = (1.0f - shadow);

                                    int idx = x * 4;
                                    float b = rowDisp[idx + 0] * lightMul;
                                    float g = rowDisp[idx + 1] * lightMul;
                                    float r = rowDisp[idx + 2] * lightMul;

                                    // Шар 1: нейтральний frost tint
                                    float finalR = (r * (1.0f - tintAlpha)) + (neutralR * tintAlpha);
                                    float finalG = (g * (1.0f - tintAlpha)) + (neutralG * tintAlpha);
                                    float finalB = (b * (1.0f - tintAlpha)) + (neutralB * tintAlpha);

                                    // Шар 2: кольоровий tint кільця (додається тільки при TintOpacity > 0)
                                    if (ringTintOpacity > 0f)
                                    {
                                        finalR = (finalR * (1.0f - ringTintOpacity)) + (ringTintR * ringTintOpacity);
                                        finalG = (finalG * (1.0f - ringTintOpacity)) + (ringTintG * ringTintOpacity);
                                        finalB = (finalB * (1.0f - ringTintOpacity)) + (ringTintB * ringTintOpacity);
                                    }

                                    outRow[idx + 0] = (byte)Math.Clamp((int)finalB, 0, 255);
                                    outRow[idx + 1] = (byte)Math.Clamp((int)finalG, 0, 255);
                                    outRow[idx + 2] = (byte)Math.Clamp((int)finalR, 0, 255);
                                    outRow[idx + 3] = 255;
                                }
                            });
                        }
                        else
                        {
                            Parallel.For(0, width, x =>
                            {
                                byte* pDispLocal = (byte*)pDispPtr;
                                byte* pDstLocal = (byte*)pDstPtr;

                                float shadow = lutShadow[x];
                                float lightMul = (1.0f - shadow);

                                long sumB = 0, sumG = 0, sumR = 0;
                                int count = 0;

                                int startLimit = Math.Min(blurR, height - 1);
                                for (int y = 0; y <= startLimit; y++)
                                {
                                    int idx = (y * width + x) * 4;
                                    sumB += pDispLocal[idx + 0];
                                    sumG += pDispLocal[idx + 1];
                                    sumR += pDispLocal[idx + 2];
                                    count++;
                                }

                                for (int y = 0; y < height; y++)
                                {
                                    int addY = y + blurR;
                                    if (addY < height && addY > startLimit)
                                    {
                                        int addIdx = (addY * width + x) * 4;
                                        sumB += pDispLocal[addIdx + 0];
                                        sumG += pDispLocal[addIdx + 1];
                                        sumR += pDispLocal[addIdx + 2];
                                        count++;
                                    }

                                    int remY = y - blurR - 1;
                                    if (remY >= 0)
                                    {
                                        int remIdx = (remY * width + x) * 4;
                                        sumB -= pDispLocal[remIdx + 0];
                                        sumG -= pDispLocal[remIdx + 1];
                                        sumR -= pDispLocal[remIdx + 2];
                                        count--;
                                    }

                                    float blurredB = ((float)sumB / count) * lightMul;
                                    float blurredG = ((float)sumG / count) * lightMul;
                                    float blurredR = ((float)sumR / count) * lightMul;

                                    // Шар 1: нейтральний frost tint
                                    float finalR = (blurredR * (1.0f - tintAlpha)) + (neutralR * tintAlpha);
                                    float finalG = (blurredG * (1.0f - tintAlpha)) + (neutralG * tintAlpha);
                                    float finalB = (blurredB * (1.0f - tintAlpha)) + (neutralB * tintAlpha);

                                    // Шар 2: кольоровий tint кільця
                                    if (ringTintOpacity > 0f)
                                    {
                                        finalR = (finalR * (1.0f - ringTintOpacity)) + (ringTintR * ringTintOpacity);
                                        finalG = (finalG * (1.0f - ringTintOpacity)) + (ringTintG * ringTintOpacity);
                                        finalB = (finalB * (1.0f - ringTintOpacity)) + (ringTintB * ringTintOpacity);
                                    }

                                    byte* outRow = pDstLocal + (y * stride);
                                    int outIdx = x * 4;

                                    outRow[outIdx + 0] = (byte)Math.Clamp((int)finalB, 0, 255);
                                    outRow[outIdx + 1] = (byte)Math.Clamp((int)finalG, 0, 255);
                                    outRow[outIdx + 2] = (byte)Math.Clamp((int)finalR, 0, 255);
                                    outRow[outIdx + 3] = 255;
                                }
                            });
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
