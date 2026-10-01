using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace CodexClipboardTranslator
{
    public static class Branding
    {
        public static readonly Color Ink = Color.FromArgb(20, 33, 51);
        public static readonly Color Teal = Color.FromArgb(109, 238, 207);
        public static readonly Color CoreWhite = Color.FromArgb(245, 249, 252);

        public static void DrawMark(Graphics graphics, RectangleF bounds)
        {
            if (graphics == null) throw new ArgumentNullException("graphics");
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float side = Math.Min(bounds.Width, bounds.Height);
                graphics.TranslateTransform(bounds.X + (bounds.Width - side) * 0.5f,
                    bounds.Y + (bounds.Height - side) * 0.5f);
                graphics.ScaleTransform(side / 100f, side / 100f);
                using (GraphicsPath tile = RoundRect(new RectangleF(4, 4, 92, 92), 23))
                using (var fill = new SolidBrush(Ink)) graphics.FillPath(fill, tile);
                DrawCore(graphics, new RectangleF(17, 15, 39, 65), CoreWhite);
                DrawCore(graphics, new RectangleF(62, 42, 24, 40), Teal);
            }
            finally { graphics.Restore(state); }
        }

        public static Bitmap Render(int size)
        {
            if (size < 1) throw new ArgumentOutOfRangeException("size");
            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            // Small tray and Explorer frames are individually supersampled.
            // No single large raster is reused for all ICO sizes.
            int sampleSize = checked(size * 4);
            using (var sample = new Bitmap(sampleSize, sampleSize, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(sample))
                { g.Clear(Color.Transparent); DrawMark(g, new RectangleF(0, 0, sampleSize, sampleSize)); }
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(sample, new Rectangle(0, 0, size, size), 0, 0,
                        sampleSize, sampleSize, GraphicsUnit.Pixel);
                }
            }
            return bitmap;
        }

        // The two cores share one silhouette: a softly slanted module with no
        // letter, speech bubble or borrowed platform symbol inside it.
        internal static void DrawCore(Graphics graphics, RectangleF bounds, Color color)
        {
            using (GraphicsPath path = CorePath(bounds))
            using (var fill = new SolidBrush(color)) graphics.FillPath(fill, path);
        }

        internal static GraphicsPath CorePath(RectangleF bounds)
        {
            var path = new GraphicsPath();
            path.AddLine(38, 0, 82, 0);
            path.AddBezier(82, 0, 95, 0, 103, 10, 99, 24);
            path.AddLine(99, 24, 80, 82);
            path.AddBezier(80, 82, 76, 94, 69, 100, 56, 100);
            path.AddLine(56, 100, 18, 100);
            path.AddBezier(18, 100, 5, 100, -3, 90, 1, 76);
            path.AddLine(1, 76, 20, 18);
            path.AddBezier(20, 18, 24, 6, 31, 0, 38, 0);
            path.CloseFigure();
            using (var transform = new Matrix(bounds.Width / 100f, 0, 0,
                bounds.Height / 100f, bounds.X, bounds.Y)) path.Transform(transform);
            return path;
        }

        public static Icon CreateIcon(int size)
        {
            using (Bitmap bitmap = Render(Math.Max(16, size)))
            {
                IntPtr handle = bitmap.GetHicon();
                try { return (Icon)Icon.FromHandle(handle).Clone(); }
                finally { DestroyIcon(handle); }
            }
        }

        // Windows supports PNG frames inside ICO. Include native small and large
        // sizes so both the notification area and Explorer avoid one-size scaling.
        public static void SaveIcon(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            byte[][] frames = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++)
                using (Bitmap bitmap = Render(sizes[i]))
                using (var memory = new MemoryStream())
                { bitmap.Save(memory, ImageFormat.Png); frames[i] = memory.ToArray(); }
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                    writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                    writer.Write(frames[i].Length); writer.Write(offset); offset += frames[i].Length;
                }
                foreach (byte[] frame in frames) writer.Write(frame);
            }
        }

        public static GraphicsPath RoundRect(RectangleF rectangle, float radius)
        {
            float diameter = Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height));
            var path = new GraphicsPath();
            path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure(); return path;
        }

        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    }
}
