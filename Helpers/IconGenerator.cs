using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CalWidget.Helpers
{
    public static class IconGenerator
    {
        public static void GenerateAppIcon(string outputPath)
        {
            int[] sizes = new[] { 256, 64, 48, 32, 16 };
            byte[][] pngData = new byte[sizes.Length][];

            for (int i = 0; i < sizes.Length; i++)
            {
                pngData[i] = RenderPng(sizes[i]);
            }

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // ICONDIR
            writer.Write((ushort)0); // reserved
            writer.Write((ushort)1); // type 1 = icon
            writer.Write((ushort)sizes.Length); // count

            int offset = 6 + (16 * sizes.Length);

            for (int i = 0; i < sizes.Length; i++)
            {
                int sz = sizes[i];
                byte w = (byte)(sz >= 256 ? 0 : sz);
                byte h = (byte)(sz >= 256 ? 0 : sz);

                writer.Write(w);
                writer.Write(h);
                writer.Write((byte)0); // color count
                writer.Write((byte)0); // reserved
                writer.Write((ushort)1); // planes
                writer.Write((ushort)32); // bpp
                writer.Write((uint)pngData[i].Length); // bytes in res
                writer.Write((uint)offset); // image offset

                offset += pngData[i].Length;
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write(pngData[i]);
            }

            writer.Flush();
            File.WriteAllBytes(outputPath, ms.ToArray());
        }

        private static byte[] RenderPng(int size)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                double scale = size / 256.0;

                // 1. Темний заокруглений Squircle фон
                var bgBrush = new LinearGradientBrush(
                    Color.FromRgb(0x1A, 0x1A, 0x20),
                    Color.FromRgb(0x0E, 0x0E, 0x12),
                    new Point(0, 0),
                    new Point(1, 1)
                );
                var bgPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), Math.Max(1.0, 1.5 * scale));
                dc.DrawRoundedRectangle(bgBrush, bgPen, new Rect(8 * scale, 8 * scale, 240 * scale, 240 * scale), 48 * scale, 48 * scale);

                Point center = new Point(128 * scale, 128 * scale);

                // 2. Зовнішнє кільце (Жири - Бурштин #F59E0B)
                DrawRingArc(dc, center, 82 * scale, 15 * scale, Color.FromRgb(0xF5, 0x9E, 0x0B), 270);

                // 3. Середнє кільце (Білки - Синій #3B82F6)
                DrawRingArc(dc, center, 58 * scale, 15 * scale, Color.FromRgb(0x3B, 0x82, 0xF6), 220);

                // 4. Внутрішнє кільце (Вуглеводи - Червоний #EF4444)
                DrawRingArc(dc, center, 34 * scale, 15 * scale, Color.FromRgb(0xEF, 0x44, 0x44), 305);

                // 5. Центр: Смарагдова крапка / серцевина (#10B981)
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)), null, center, 11 * scale, 11 * scale);
            }

            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var outMs = new MemoryStream();
            encoder.Save(outMs);
            return outMs.ToArray();
        }

        private static void DrawRingArc(DrawingContext dc, Point center, double radius, double thickness, Color color, double sweepAngleDeg)
        {
            var pen = new Pen(new SolidColorBrush(color), thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };

            double startAngleDeg = -90;
            double endAngleDeg = startAngleDeg + sweepAngleDeg;
            double startRad = startAngleDeg * Math.PI / 180.0;
            double endRad = endAngleDeg * Math.PI / 180.0;

            Point startPoint = new Point(center.X + radius * Math.Cos(startRad), center.Y + radius * Math.Sin(startRad));
            Point endPoint = new Point(center.X + radius * Math.Cos(endRad), center.Y + radius * Math.Sin(endRad));

            var figure = new PathFigure
            {
                StartPoint = startPoint,
                IsClosed = false
            };
            figure.Segments.Add(new ArcSegment
            {
                Point = endPoint,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = sweepAngleDeg > 180.0
            });

            var geom = new PathGeometry();
            geom.Figures.Add(figure);
            dc.DrawGeometry(null, pen, geom);
        }
    }
}
