using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CalWidget.Helpers
{
    public static class RingGeometryHelper
    {
        /// <summary>
        /// Створює або оновлює дугу кільця за тригонометричною формулою (X = CenterX + R * cos, Y = CenterY + R * sin).
        /// Початок дуги строго о 12:00 (-90 градусів).
        /// </summary>
        public static void UpdateArcPath(Path? path, Point center, double radius, double progress)
        {
            if (path == null) return;

            // Якщо прогрес практично нульовий - приховуємо дугу
            if (progress <= 0.0005)
            {
                path.Visibility = Visibility.Collapsed;
                return;
            }

            path.Visibility = Visibility.Visible;

            // Обмеження до 0.9999 (359.96 градусів): у WPF ArcSegment схлопується при 360 градусах
            double clampedProgress = Math.Clamp(progress, 0.0005, 0.9999);
            double angleInDegrees = clampedProgress * 360.0;
            // Початок о 12:00: відлік від -90 градусів (-PI/2)
            double angleInRadians = (angleInDegrees - 90.0) * (Math.PI / 180.0);

            // Початкова точка дуги (12:00 зверху)
            Point startPoint = new Point(center.X, center.Y - radius);

            // Кінцева точка дуги за тригонометричною формулою
            Point endPoint = new Point(
                center.X + radius * Math.Cos(angleInRadians),
                center.Y + radius * Math.Sin(angleInRadians)
            );

            bool isLargeArc = angleInDegrees > 180.0;

            // Швидке оновлення існуючого ArcSegment без зайвих виділень пам'яті на кожному кадрі
            if (path.Data is PathGeometry geom &&
                geom.Figures.Count > 0 &&
                geom.Figures[0].Segments.Count > 0 &&
                geom.Figures[0].Segments[0] is ArcSegment existingArc)
            {
                geom.Figures[0].StartPoint = startPoint;
                existingArc.Size = new Size(radius, radius);
                existingArc.Point = endPoint;
                existingArc.IsLargeArc = isLargeArc;
            }
            else
            {
                var figure = new PathFigure
                {
                    StartPoint = startPoint,
                    IsClosed = false
                };
                var newArc = new ArcSegment
                {
                    Point = endPoint,
                    Size = new Size(radius, radius),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = isLargeArc
                };
                figure.Segments.Add(newArc);

                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);
                path.Data = geometry;
            }
        }

        public static PathGeometry CreateArcGeometry(Point center, double radius, double progress)
        {
            var geometry = new PathGeometry();
            if (progress <= 0.0005)
                return geometry;

            double clampedProgress = Math.Clamp(progress, 0.0005, 0.9999);
            double angleInDegrees = clampedProgress * 360.0;
            double angleInRadians = (angleInDegrees - 90.0) * (Math.PI / 180.0);

            Point startPoint = new Point(center.X, center.Y - radius);
            Point endPoint = new Point(
                center.X + radius * Math.Cos(angleInRadians),
                center.Y + radius * Math.Sin(angleInRadians)
            );

            bool isLargeArc = angleInDegrees > 180.0;

            var figure = new PathFigure
            {
                StartPoint = startPoint,
                IsClosed = false
            };

            var arc = new ArcSegment
            {
                Point = endPoint,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = isLargeArc
            };

            figure.Segments.Add(arc);
            geometry.Figures.Add(figure);
            return geometry;
        }
    }
}
