using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace CodexClipboardTranslator
{
    // Original companion-module geometry; the caller owns timing and repaint scheduling.
    // The loop signals activity, with no angular rotation or simulated progress.
    public static class MotionGlyph
    {
        public const double CompletionDurationSeconds = 0.42;

        public static void DrawProcessing(Graphics g, RectangleF bounds, double elapsedSeconds)
        {
            if (g == null) throw new ArgumentNullException("g");
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            GraphicsState state = g.Save();
            try
            {
                MapBounds(g, bounds);
                double seconds = FiniteElapsed(elapsedSeconds);
                double phase = (seconds % 1.65) / 1.65 * Math.PI * 2.0;
                // Cosine ease-in-out keeps velocity continuous across the loop.
                // A quarter-cycle handoff follows the larger core rather than
                // synchronously bouncing both shapes. A few device-independent
                // pixels of shape change keep the relay visible at native size.
                float main = (float)(0.5 - 0.5 * Math.Cos(phase));
                float echo = (float)(0.5 - 0.5 * Math.Cos(phase - Math.PI * 0.5));
                using (GraphicsPath tile = Branding.RoundRect(new RectangleF(1.2f, 1.2f, 27.6f, 27.6f), 6.9f))
                using (var fill = new SolidBrush(Branding.Ink)) g.FillPath(fill, tile);

                Branding.DrawCore(g, new RectangleF(5.1f + main * 0.35f,
                    4.5f + main * 1.8f, 11.7f + main * 1.4f,
                    19.5f - main * 3.6f), Branding.CoreWhite);
                Branding.DrawCore(g, new RectangleF(18.6f - echo * 0.65f,
                    12.6f - echo * 2.3f, 7.2f + echo * 0.7f,
                    12f + echo * 3.0f), Branding.Teal);
            }
            finally { g.Restore(state); }
        }

        public static void DrawCompleted(Graphics g, RectangleF bounds, double elapsedSeconds)
        {
            if (g == null) throw new ArgumentNullException("g");
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            GraphicsState state = g.Save();
            try
            {
                MapBounds(g, bounds);
                using (var fill = new SolidBrush(Color.FromArgb(229, 243, 235)))
                    g.FillEllipse(fill, 0, 0, 30, 30);

                // The caller owns time and repaint scheduling; >= 0.42s is fully static.
                double seconds = Double.IsPositiveInfinity(elapsedSeconds)
                    ? CompletionDurationSeconds : FiniteElapsed(elapsedSeconds);
                double settle = EaseOut(Clamp01(seconds / CompletionDurationSeconds));
                float scale = (float)(0.96 + 0.04 * settle);
                g.TranslateTransform(15, 15);
                g.ScaleTransform(scale, scale);
                g.TranslateTransform(-15, -15);

                double reveal = EaseOut(Clamp01(seconds / 0.26));
                int alpha = (int)Math.Round(255.0 * EaseOut(Clamp01(seconds / 0.10)));
                if (reveal <= 0 || alpha <= 0) return;
                PointF first = new PointF(8, 15);
                PointF elbow = new PointF(12.5f, 19.5f);
                PointF last = new PointF(22, 9.5f);
                double firstLength = Distance(first, elbow);
                double lastLength = Distance(elbow, last);
                double visibleLength = (firstLength + lastLength) * reveal;
                using (var path = new GraphicsPath())
                using (var pen = RoundedPen(Color.FromArgb(alpha, 35, 135, 93), 2.3f))
                {
                    if (visibleLength <= firstLength)
                        path.AddLine(first, Interpolate(first, elbow, visibleLength / firstLength));
                    else
                    {
                        path.AddLine(first, elbow);
                        path.AddLine(elbow, Interpolate(elbow, last,
                            (visibleLength - firstLength) / lastLength));
                    }
                    g.DrawPath(pen, path);
                }
            }
            finally { g.Restore(state); }
        }

        private static void MapBounds(Graphics g, RectangleF bounds)
        {
            float side = Math.Min(bounds.Width, bounds.Height);
            g.TranslateTransform(bounds.X + (bounds.Width - side) * 0.5f,
                bounds.Y + (bounds.Height - side) * 0.5f);
            g.ScaleTransform(side / 30f, side / 30f);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        private static Pen RoundedPen(Color color, float width)
        {
            var pen = new Pen(color, width);
            pen.StartCap = pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            return pen;
        }

        private static double FiniteElapsed(double seconds)
        {
            return Double.IsNaN(seconds) || Double.IsInfinity(seconds) || seconds < 0 ? 0 : seconds;
        }
        private static double Clamp01(double value) { return Math.Max(0, Math.Min(1, value)); }
        private static double EaseOut(double value) { return 1.0 - Math.Pow(1.0 - value, 3); }
        private static double Distance(PointF first, PointF last)
        {
            double x = last.X - first.X, y = last.Y - first.Y;
            return Math.Sqrt(x * x + y * y);
        }
        private static PointF Interpolate(PointF first, PointF last, double fraction)
        {
            return new PointF((float)(first.X + (last.X - first.X) * fraction),
                (float)(first.Y + (last.Y - first.Y) * fraction));
        }
    }
}
