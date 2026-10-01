using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CodexClipboardTranslator
{
    // One buffered surface: no child HWNDs and no recurring whole-window animation.
    public sealed class Popup : Form
    {
        private const int LogicalWidth = 264, LogicalHeight = 64;
        private static readonly Color Paper = Color.FromArgb(253, 253, 250);
        private static readonly Color Ink = Color.FromArgb(31, 47, 45);
        private static readonly Color Muted = Color.FromArgb(102, 119, 116);
        private static readonly Color Teal = Color.FromArgb(18, 122, 111);
        private static readonly RectangleF CloseArea = new RectangleF(230, 16, 28, 32);
        private static readonly RectangleF CopyArea = new RectangleF(170, 16, 56, 32);
        private static readonly RectangleF FooterArea = new RectangleF(158, 16, 70, 34);
        private static readonly RectangleF MotionArea = new RectangleF(10, 12, 40, 40);
        private readonly System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer motionTimer = new System.Windows.Forms.Timer();
        private readonly Font headingFont = new Font("Microsoft YaHei UI", 20f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font smallFont = new Font("Microsoft YaHei UI", 13.5f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font timerFont = new Font("Microsoft YaHei UI", 18f, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font actionFont = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
        private bool working, completed, copied, error;
        private string heading = "", message = "", footer = "";
        private long startedAt, hideAt, stateStartedAt;
        private float uiScale = 1f;
        private int hoveredAction, pressedAction, keyboardAction, paintCount, fullPaintCount;
        public event Action CopyRequested;

        public Popup()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Paper;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(LogicalWidth, LogicalHeight);
            AccessibleName = "Codex 伴随翻译提示";
            AccessibleRole = AccessibleRole.Alert;
            uiTimer.Interval = 250;
            uiTimer.Tick += OnUiTick;
            motionTimer.Interval = 16;
            motionTimer.Tick += OnMotionTick;
            UpdateWindowRegion();
        }

        // These expose real state for offline UI regression tests, without clipboard access.
        public bool Working { get { return working; } }
        public bool Completed { get { return completed; } }
        public bool UiTimerRunning { get { return uiTimer.Enabled; } }
        public int PaintCount { get { return paintCount; } }
        public int FullPaintCount { get { return fullPaintCount; } }
        public bool AnimationRunning { get { return motionTimer.Enabled; } }
        public float TimerFontPixels { get { return timerFont.Size; } }
        public float UiScale { get { return uiScale; } }
        public Rectangle CopyBounds { get { return PhysicalRectangle(CopyArea); } }
        public Rectangle CloseBounds { get { return PhysicalRectangle(CloseArea); } }
        public string StatusText { get { return footer; } }
        public bool CanCopy { get { return completed && !copied; } }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams p = base.CreateParams;
                p.Style &= ~0x00C40000; // No native caption, border or resize frame.
                p.ExStyle &= ~0x00020300; // No static, client or window edge.
                p.ExStyle |= 0x08000000 | 0x80;
                p.ClassStyle &= ~0x00020000; return p;
            }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int disabled = 1; // DWMNCRP_DISABLED: custom client border only.
                DwmSetWindowAttribute(Handle, 2, ref disabled, sizeof(int));
                int noBorder = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE, where supported.
                DwmSetWindowAttribute(Handle, 34, ref noBorder, sizeof(int));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0021) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE; deliver the click.
            if (m.Msg == 0x0085) { m.Result = IntPtr.Zero; return; } // Client-owned rounded border; no separate frame paint.
            base.WndProc(ref m);
        }

        public void Processing(string text)
        {
            BeginState();
            working = true;
            startedAt = Stopwatch.GetTimestamp();
            heading = "正在翻译";
            message = "";
            footer = "0 秒";
            currentStage = text ?? "正在准备翻译";
            Reveal();
        }
        private string currentStage = "";
        public void Progress(string text)
        {
            // Queued backend progress must not overwrite a completed/error card or reopen a dismissed card.
            if (!working || IsDisposed || text == null || text == currentStage) return;
            currentStage = text;
            RefreshElapsed();
        }
        public void Result(string translation, bool wasCopied, int seconds)
        {
            BeginState();
            completed = true;
            copied = wasCopied;
            // The popup never stores or renders translation content. CopyRequested
            // uses the complete result retained by TranslationContext.
            heading = copied ? "已复制" : "翻译完成";
            message = "";
            footer = copied ? "已替换剪贴板" : "新剪贴板内容已保留";
            SetExpiry(seconds);
            Reveal();
        }
        public void Notice(string title, string text, bool isError, int seconds)
        {
            BeginState();
            error = isError;
            heading = title ?? "提示";
            const string brandPrefix = "Codex 翻译";
            if (heading.StartsWith(brandPrefix, StringComparison.Ordinal)) heading = heading.Substring(brandPrefix.Length).Trim();
            message = text ?? "";
            footer = error ? "原剪贴板未改动，可重试" : "工具继续在托盘运行";
            SetExpiry(seconds);
            Reveal();
        }
        private void BeginState()
        {
            // A timer tick always consults the current deadline. No stale expiry callback survives a new state.
            uiTimer.Stop();
            motionTimer.Stop();
            stateStartedAt = Stopwatch.GetTimestamp();
            hideAt = 0;
            working = completed = copied = error = false;
            hoveredAction = pressedAction = keyboardAction = 0;
            Cursor = Cursors.Default;
        }
        private void SetExpiry(int seconds)
        {
            hideAt = Stopwatch.GetTimestamp() + (long)(Math.Max(1, Math.Min(120, seconds)) * (double)Stopwatch.Frequency);
        }
        private void Reveal()
        {
            Rectangle area = Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
            // Move onto the target monitor before asking the actual window for
            // its DPI. GetDpiForMonitor is unsuitable for a PM-aware thread.
            Location = new Point(Math.Max(area.Left, area.Right - Width - 20), Math.Max(area.Top, area.Bottom - Height - 20));
            float scale = ReadWindowScale();
            scale = Math.Min(scale, Math.Min(Math.Max(1, area.Width - 24) / (float)LogicalWidth,
                Math.Max(1, area.Height - 24) / (float)LogicalHeight));
            scale = Math.Max(0.5f, scale);
            if (Math.Abs(uiScale - scale) > 0.01f)
            {
                uiScale = scale;
                ClientSize = new Size((int)Math.Round(LogicalWidth * uiScale), (int)Math.Round(LogicalHeight * uiScale));
                UpdateWindowRegion();
            }
            int margin = (int)Math.Round(20 * uiScale);
            int x = Math.Max(area.Left, Math.Min(area.Right - Width, area.Right - Width - margin));
            int y = Math.Max(area.Top, Math.Min(area.Bottom - Height, area.Bottom - Height - margin));
            Location = new Point(x, y);
            AccessibleDescription = completed ? "英文译文已完成。" + footer + (CanCopy ? "。可复制译文或关闭提示。" : "。可以直接粘贴或关闭提示。") : heading + "。" + message;
            if (!Visible) Show();
            uiTimer.Start();
            if (working || completed && StateSeconds < MotionGlyph.CompletionDurationSeconds) motionTimer.Start();
            Invalidate(); // Only state transitions request a complete repaint.
            AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible) { uiTimer.Stop(); motionTimer.Stop(); }
            else if (working || hideAt != 0)
            {
                uiTimer.Start();
                if (working || completed && StateSeconds < MotionGlyph.CompletionDurationSeconds) motionTimer.Start();
            }
        }
        private double StateSeconds { get { return (Stopwatch.GetTimestamp() - stateStartedAt) / (double)Stopwatch.Frequency; } }
        private void OnMotionTick(object sender, EventArgs e)
        {
            if (!Visible || !working && !completed) { motionTimer.Stop(); return; }
            if (completed && StateSeconds >= MotionGlyph.CompletionDurationSeconds) motionTimer.Stop();
            Invalidate(PhysicalRectangle(MotionArea));
        }
        private void OnUiTick(object sender, EventArgs e)
        {
            if (!Visible) { uiTimer.Stop(); return; }
            if (hideAt != 0 && Stopwatch.GetTimestamp() >= hideAt) { Hide(); return; }
            if (working) RefreshElapsed();
        }
        private void RefreshElapsed()
        {
            string updated = (int)((Stopwatch.GetTimestamp() - startedAt) / (double)Stopwatch.Frequency) + " 秒";
            if (updated == footer) return;
            footer = updated;
            if (Visible) Invalidate(PhysicalRectangle(FooterArea));
        }

        // Direct rendering never shows a window, changes DPI, reads credentials, or touches the clipboard.
        public void RenderPreview(string path) { RenderPreview(path, uiScale); }
        public void RenderPreview(string path, float scale)
        { RenderPreviewAtTime(path, scale, completed ? MotionGlyph.CompletionDurationSeconds : 0.8); }
        public void RenderPreviewAtTime(string path, float scale, double stateSeconds)
        {
            if (scale < 0.5f || scale > 4f) throw new ArgumentOutOfRangeException("scale");
            using (var bitmap = new Bitmap((int)Math.Round(LogicalWidth * scale), (int)Math.Round(LogicalHeight * scale)))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Paper);
                g.ScaleTransform(scale, scale);
                DrawCard(g, stateSeconds);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        protected override void OnPaintBackground(PaintEventArgs e) { } // OnPaint owns every pixel; no erase pass.
        protected override void OnPaint(PaintEventArgs e)
        {
            paintCount++;
            if (e.ClipRectangle.Contains(ClientRectangle)) fullPaintCount++;
            e.Graphics.ScaleTransform(uiScale, uiScale);
            DrawCard(e.Graphics, StateSeconds);
            base.OnPaint(e);
        }
        private void DrawCard(Graphics g, double stateSeconds)
        {
            // Cover the backing buffer fully before antialiasing the shape.
            // Antialiased FillRectangle leaves partial top/left pixel coverage,
            // which previously exposed the dark uninitialized buffer edge.
            g.SmoothingMode = SmoothingMode.None;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (var paper = new SolidBrush(Paper)) g.FillRectangle(paper, 0, 0, LogicalWidth, LogicalHeight);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawBorder(g);
            DrawStatus(g, stateSeconds);
            DrawClose(g);
            bool detail = !working && !completed && !String.IsNullOrWhiteSpace(message);
            float titleWidth = working ? 103 : CanCopy ? 112 : 172;
            DrawText(g, heading, headingFont, Ink, new RectangleF(54, detail ? 6 : 18, titleWidth, 32), false);
            if (detail)
            {
                DrawText(g, message, smallFont, Muted, new RectangleF(54, 38, 172, 21), false);
            }
            if (working) DrawText(g, (int)Math.Max(0, stateSeconds) + " 秒", timerFont, Ink, new RectangleF(160, 16, 66, 34), false);
            if (CanCopy) DrawCopy(g);
        }
        private void DrawStatus(Graphics g, double stateSeconds)
        {
            if (working) { MotionGlyph.DrawProcessing(g, new RectangleF(15, 17, 30, 30), stateSeconds); return; }
            if (completed) { MotionGlyph.DrawCompleted(g, new RectangleF(15, 17, 30, 30), stateSeconds); return; }
            Color dot = working ? Color.FromArgb(171, 117, 16) : error ? Color.FromArgb(185, 65, 65) : completed ? Color.FromArgb(35, 135, 93) : Teal;
            Color tint = working ? Color.FromArgb(252, 242, 213) : error ? Color.FromArgb(252, 235, 233) : Color.FromArgb(229, 243, 235);
            if (!working && !completed && !error)
            { Branding.DrawMark(g, new RectangleF(16, 17, 30, 30)); return; }
            using (var fill = new SolidBrush(tint)) g.FillEllipse(fill, 15, 17, 30, 30);
            using (var pen = new Pen(dot, 2.3f))
            {
                pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                if (completed) g.DrawLines(pen, new[] { new PointF(23, 32), new PointF(28, 37), new PointF(38, 26) });
                else if (working)
                { g.DrawEllipse(pen, 22, 24, 16, 16); g.DrawLine(pen, 30, 27, 30, 32); g.DrawLine(pen, 30, 32, 34, 34); }
                else { g.DrawLine(pen, 30, 25, 30, 33); using (var fill = new SolidBrush(dot)) g.FillEllipse(fill, 28.5f, 36, 3, 3); }
            }
        }
        private void DrawClose(Graphics g)
        {
            if (hoveredAction == 1 || pressedAction == 1 || keyboardAction == 1)
                using (GraphicsPath shape = Rounded(CloseArea, 9))
                using (var fill = new SolidBrush(Color.FromArgb(235, 240, 234))) g.FillPath(fill, shape);
            using (var pen = new Pen(Muted, 1.4f))
            {
                g.DrawLine(pen, 239, 27, 249, 37);
                g.DrawLine(pen, 249, 27, 239, 37);
            }
        }
        private void DrawCopy(Graphics g)
        {
            Color background = pressedAction == 2 ? Color.FromArgb(15, 102, 92) : hoveredAction == 2 ? Color.FromArgb(22, 139, 125) : Teal;
            using (GraphicsPath shape = Rounded(CopyArea, 8))
            using (var fill = new SolidBrush(background)) g.FillPath(fill, shape);
            DrawText(g, "复制", actionFont, Color.White, new RectangleF(179, 21, 39, 24), false);
            if (keyboardAction == 2)
                using (var pen = new Pen(Color.White)) g.DrawRectangle(pen, 174, 20, 48, 24);
        }
        private static void DrawText(Graphics g, string text, Font font, Color color, RectangleF bounds, bool wrap)
        {
            if (!g.IsVisible(bounds)) return;
            PointF[] points = { new PointF(bounds.Left, bounds.Top), new PointF(bounds.Right, bounds.Bottom), new PointF(0, 0), new PointF(0, 1) };
            g.TransformPoints(CoordinateSpace.Device, CoordinateSpace.World, points);
            float pixelScale = (float)Math.Sqrt(Math.Pow(points[3].X - points[2].X, 2) + Math.Pow(points[3].Y - points[2].Y, 2));
            Rectangle physical = Rectangle.FromLTRB((int)Math.Round(points[0].X), (int)Math.Round(points[0].Y),
                (int)Math.Round(points[1].X), (int)Math.Round(points[1].Y));
            GraphicsState saved = g.Save();
            try
            {
                g.ResetTransform(); g.PageUnit = GraphicsUnit.Pixel; g.PageScale = 1f;
                using (var physicalFont = new Font(font.FontFamily, font.Size * pixelScale, font.Style, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(color))
                using (var format = new StringFormat(StringFormat.GenericTypographic))
                {
                    // GDI TextRenderer produced visibly aliased strokes in the
                    // target environment. Render outline text at device pixels,
                    // with grayscale antialiasing and no subpixel color fringes.
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    format.Trimming = StringTrimming.EllipsisCharacter;
                    format.LineAlignment = StringAlignment.Center;
                    format.FormatFlags = StringFormatFlags.LineLimit | (wrap ? (StringFormatFlags)0 : StringFormatFlags.NoWrap);
                    g.DrawString(text ?? "", physicalFont, brush, physical, format);
                }
            }
            finally { g.Restore(saved); }
        }
        private static void DrawBorder(Graphics g)
        {
            PointF[] points = { new PointF(0, 0), new PointF(LogicalWidth, LogicalHeight) };
            g.TransformPoints(CoordinateSpace.Device, CoordinateSpace.World, points);
            float width = (float)Math.Round(points[1].X - points[0].X), height = (float)Math.Round(points[1].Y - points[0].Y);
            float scale = width / LogicalWidth;
            GraphicsState saved = g.Save();
            try
            {
                g.ResetTransform(); g.PageUnit = GraphicsUnit.Pixel; g.PageScale = 1f;
                using (GraphicsPath outline = Rounded(new RectangleF(points[0].X + 0.5f, points[0].Y + 0.5f, width - 1, height - 1), 14 * scale - 0.5f))
                using (var pen = new Pen(Color.FromArgb(221, 228, 225), 1f)) g.DrawPath(pen, outline);
            }
            finally { g.Restore(saved); }
        }
        private static GraphicsPath Rounded(RectangleF r, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
        private Rectangle PhysicalRectangle(RectangleF r)
        {
            return Rectangle.Ceiling(new RectangleF(r.X * uiScale, r.Y * uiScale, r.Width * uiScale, r.Height * uiScale));
        }
        private void UpdateWindowRegion()
        {
            using (GraphicsPath path = Rounded(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), 14 * uiScale))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }
        private int Hit(Point point)
        {
            if (CloseBounds.Contains(point)) return 1;
            return CanCopy && CopyBounds.Contains(point) ? 2 : 0;
        }
        private void RefreshAction(int action)
        {
            if (action != 0 && Visible) Invalidate(action == 1 ? CloseBounds : CopyBounds);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int next = Hit(e.Location);
            if (next != hoveredAction) { int old = hoveredAction; hoveredAction = next; RefreshAction(old); RefreshAction(next); }
            Cursor = next == 0 ? Cursors.Default : Cursors.Hand;
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            int old = hoveredAction; hoveredAction = 0; RefreshAction(old); Cursor = Cursors.Default;
            base.OnMouseLeave(e);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { pressedAction = Hit(e.Location); Capture = pressedAction != 0; RefreshAction(pressedAction); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int action = pressedAction; pressedAction = 0; Capture = false; RefreshAction(action);
                if (action != 0 && action == Hit(e.Location)) InvokeAction(action);
            }
            base.OnMouseUp(e);
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture && pressedAction != 0) { int old = pressedAction; pressedAction = 0; RefreshAction(old); }
            base.OnMouseCaptureChanged(e);
        }
        private void InvokeAction(int action)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke((Action)(() => InvokeAction(action))); return; }
            if (action == 1) Hide();
            else if (action == 2 && CanCopy) { Action handler = CopyRequested; if (handler != null) handler(); }
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Normally the popup never takes keyboard focus. These also support explicit assistive focus.
            if (keyData == Keys.Escape) { Hide(); return true; }
            if (CanCopy && keyData == (Keys.Control | Keys.C)) { InvokeAction(2); return true; }
            if (keyData == Keys.Enter || keyData == Keys.Space)
            { InvokeAction(keyboardAction == 2 && CanCopy ? 2 : 1); return true; }
            if (keyData == Keys.Tab || keyData == (Keys.Shift | Keys.Tab))
            {
                int old = keyboardAction; keyboardAction = CanCopy && keyboardAction != 2 ? 2 : 1;
                RefreshAction(old); RefreshAction(keyboardAction); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        protected override AccessibleObject CreateAccessibilityInstance() { return new PopupAccessibility(this); }
        private sealed class PopupAccessibility : ControlAccessibleObject
        {
            private readonly Popup owner;
            public PopupAccessibility(Popup popup) : base(popup) { owner = popup; }
            public override int GetChildCount() { return owner.CanCopy ? 2 : 1; }
            public override AccessibleObject GetChild(int index)
            {
                if (index == 0) return new ActionAccessibility(owner, 1, this);
                return index == 1 && owner.CanCopy ? new ActionAccessibility(owner, 2, this) : null;
            }
        }
        private sealed class ActionAccessibility : AccessibleObject
        {
            private readonly Popup owner;
            private readonly int action;
            private readonly AccessibleObject parent;
            public ActionAccessibility(Popup popup, int value, AccessibleObject container) { owner = popup; action = value; parent = container; }
            public override string Name { get { return action == 1 ? "关闭提示" : owner.copied ? "已复制，再次复制译文" : "复制译文"; } set { } }
            public override string Description { get { return action == 1 ? "仅隐藏提示，翻译继续进行。" : "将完整英文译文复制到剪贴板。"; } }
            public override string DefaultAction { get { return action == 1 ? "关闭" : "复制"; } }
            public override AccessibleRole Role { get { return AccessibleRole.PushButton; } }
            public override AccessibleStates State { get { return owner.Visible ? AccessibleStates.Focusable : AccessibleStates.Invisible; } }
            public override AccessibleObject Parent { get { return parent; } }
            public override Rectangle Bounds { get { return owner.RectangleToScreen(action == 1 ? owner.CloseBounds : owner.CopyBounds); } }
            public override void DoDefaultAction() { owner.InvokeAction(action); }
        }
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int bytes);
        private float ReadWindowScale()
        {
            try
            {
                uint dpi = GetDpiForWindow(Handle);
                if (dpi > 0) return dpi / 96f;
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                uiTimer.Stop(); uiTimer.Dispose();
                motionTimer.Stop(); motionTimer.Dispose();
                headingFont.Dispose(); smallFont.Dispose(); timerFont.Dispose(); actionFont.Dispose();
                Region old = Region; Region = null; if (old != null) old.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
