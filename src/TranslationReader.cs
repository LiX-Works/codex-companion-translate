using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CodexClipboardTranslator
{
    // A persistent reader: the controller owns translations and clipboard writes.
    // All dimensions and pixel fonts use one explicit DIP-to-pixel scale.
    public sealed class TranslationReader : Form
    {
        private const float FontPoints = 14f;
        private static readonly Color Paper = Color.FromArgb(253, 252, 248);
        private static readonly Color Ink = Color.FromArgb(31, 47, 45);
        private static readonly Color Muted = Color.FromArgb(100, 116, 112);
        private static readonly Color Rule = Color.FromArgb(227, 231, 225);
        private static readonly Color Teal = Color.FromArgb(18, 112, 100);
        private readonly RichTextBox content = new RichTextBox();
        private readonly Button copy = new Button();
        private readonly Button close = new Button();
        private Font readerFont, titleFont, captionFont, buttonFont;
        private Icon readerIcon;
        private float uiScale = 1f;
        private string fullText = "";
        private bool copied, fitting, disposing, hasLayout;
        private int textHeight;
        public event Action CopyRequested;

        public TranslationReader()
        {
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            MaximizeBox = false;
            MinimizeBox = false;
            Text = "中文译文 · Codex 伴随翻译";
            AccessibleName = "中文译文阅读窗口";
            AccessibleDescription = "阅读完整译文；复制全文按钮复制译文；按 Esc 隐藏窗口。";
            BackColor = Paper;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
            KeyPreview = true;

            content.Name = "TranslationContent";
            content.AccessibleName = "完整中文译文";
            content.AccessibleRole = AccessibleRole.Text;
            content.ReadOnly = true;
            content.BorderStyle = BorderStyle.None;
            content.BackColor = Paper;
            content.ForeColor = Ink;
            content.WordWrap = true;
            content.DetectUrls = false;
            content.ScrollBars = RichTextBoxScrollBars.Vertical;
            content.HideSelection = false;
            content.ShortcutsEnabled = true;
            content.MaxLength = Int32.MaxValue;
            content.TabIndex = 0;

            copy.Name = "CopyFullTranslation";
            copy.Text = "复制全文";
            copy.AccessibleName = "复制完整中文译文";
            copy.AccessibleDescription = "可重复复制完整译文到剪贴板。";
            copy.TabIndex = 1;
            ConfigureButton(copy, Teal, Color.White);
            copy.FlatAppearance.MouseOverBackColor = Color.FromArgb(23, 126, 112);
            copy.FlatAppearance.MouseDownBackColor = Color.FromArgb(14, 90, 82);
            copy.Click += delegate { Action handler = CopyRequested; if (handler != null) handler(); };

            close.Name = "HideReader";
            close.Text = "关闭 · Esc";
            close.AccessibleName = "关闭阅读窗口";
            close.AccessibleDescription = "隐藏阅读窗口，翻译工具继续运行。";
            close.TabIndex = 2;
            ConfigureButton(close, Paper, Muted);
            close.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 242, 235);
            close.FlatAppearance.MouseDownBackColor = Color.FromArgb(231, 235, 227);
            close.Click += delegate { Hide(); };

            Controls.Add(content);
            Controls.Add(copy);
            Controls.Add(close);
            readerIcon = Branding.CreateIcon(32);
            Icon = readerIcon;
            ApplyScale(1f);
            ClientSize = new Size(S(720), S(290));
            hasLayout = true;
            ArrangeControls();
        }

        public string ContentText { get { return fullText; } }
        public bool UsesVerticalScroll { get { return textHeight > content.ClientSize.Height; } }
        public float ReaderFontPoints { get { return FontPoints; } }

        public void Present(string translation, bool wasCopied)
        {
            if (IsDisposed) throw new ObjectDisposedException("TranslationReader");
            fullText = translation ?? "";
            copied = wasCopied;
            Screen screen = Screen.FromPoint(Cursor.Position);
            Rectangle area = screen.WorkingArea;
            // Moving to the target monitor before querying the window DPI makes
            // mixed-DPI displays follow the same single-scale layout contract.
            Location = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
            IntPtr window = Handle;
            ApplyScale(WindowScale(window));
            content.Text = fullText;
            copy.Enabled = fullText.Length != 0;
            content.Select(0, 0);
            FitToScreen(area);
            Location = new Point(area.Left + (area.Width - Width) / 2,
                area.Top + (area.Height - Height) / 2);
            if (!Visible) Show();
            BringToFront();
            Activate();
            SetForegroundWindow(Handle);
            content.Focus();
            content.Select(0, 0);
            content.ScrollToCaret();
            Invalidate();
        }

        // The controller may update the footer after a successful repeat copy,
        // without resetting the selection, reading position or window geometry.
        public void SetCopied(bool value)
        {
            copied = value;
            Invalidate(new Rectangle(0, Math.Max(0, ClientSize.Height - S(88)), ClientSize.Width, S(88)));
        }

        private static void ConfigureButton(Button button, Color background, Color foreground)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = background;
            button.ForeColor = foreground;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
        }

        private int S(float value) { return (int)Math.Round(value * uiScale); }

        private void ApplyScale(float value)
        {
            value = Math.Max(0.75f, Math.Min(4f, value));
            if (readerFont != null && Math.Abs(uiScale - value) < 0.001f) return;
            uiScale = value;
            Font oldReader = readerFont, oldTitle = titleFont, oldCaption = captionFont, oldButton = buttonFont;
            readerFont = new Font("Microsoft YaHei UI", FontPoints * 96f / 72f * uiScale, FontStyle.Regular, GraphicsUnit.Pixel);
            titleFont = new Font("Microsoft YaHei UI", 26f * uiScale, FontStyle.Bold, GraphicsUnit.Pixel);
            captionFont = new Font("Microsoft YaHei UI", 12f * uiScale, FontStyle.Regular, GraphicsUnit.Pixel);
            buttonFont = new Font("Microsoft YaHei UI", 14f * uiScale, FontStyle.Bold, GraphicsUnit.Pixel);
            content.Font = readerFont;
            copy.Font = buttonFont;
            close.Font = buttonFont;
            if (oldReader != null) oldReader.Dispose();
            if (oldTitle != null) oldTitle.Dispose();
            if (oldCaption != null) oldCaption.Dispose();
            if (oldButton != null) oldButton.Dispose();
            // Include the native frame when defining the minimum window size.
            MinimumSize = SizeFromClientSize(new Size(S(420), S(230)));
            if (hasLayout) ArrangeControls();
            Invalidate();
        }

        private void FitToScreen(Rectangle area)
        {
            fitting = true;
            try
            {
                int frameWidth = Width - ClientSize.Width, frameHeight = Height - ClientSize.Height;
                int maxClientWidth = Math.Max(1, (int)(area.Width * 0.80) - frameWidth);
                int maxClientHeight = Math.Max(1, (int)(area.Height * 0.80) - frameHeight);
                // A small monitor must still win over the preferred minimum.
                MinimumSize = new Size(Math.Min(MinimumSize.Width, maxClientWidth + frameWidth),
                    Math.Min(MinimumSize.Height, maxClientHeight + frameHeight));
                int width = Math.Min(S(720), maxClientWidth);
                ClientSize = new Size(width, Math.Min(S(290), maxClientHeight));
                ArrangeControls();
                int measured = MeasureContentHeight();
                int desired = S(104 + 88) + measured;
                ClientSize = new Size(width, Math.Min(maxClientHeight, Math.Max(S(230), desired)));
                ArrangeControls();
                textHeight = MeasureContentHeight();
                content.Select(0, 0);
                content.ScrollToCaret();
            }
            finally { fitting = false; }
        }

        private int MeasureContentHeight()
        {
            // Native RichEdit wraps URLs and CJK according to its real font and
            // formatting rectangle. Counting its visual lines avoids estimating
            // window height from character count or a differently shaped font.
            int lineCount = SendMessage(content.Handle, 0x00BA, IntPtr.Zero, IntPtr.Zero).ToInt32(); // EM_GETLINECOUNT
            Point first = content.GetPositionFromCharIndex(0);
            Point last = content.GetPositionFromCharIndex(Math.Max(0, content.TextLength - 1));
            int lineHeight = (int)Math.Ceiling(readerFont.GetHeight());
            if (lineCount > 1)
            {
                int nextIndex = content.GetFirstCharIndexFromLine(1);
                if (nextIndex >= 0)
                {
                    int nativeHeight = content.GetPositionFromCharIndex(nextIndex).Y - first.Y;
                    if (nativeHeight > 0) lineHeight = nativeHeight;
                }
            }
            // Include a final blank line and a little breathing room below the
            // descenders. The native position also handles paragraph spacing.
            return Math.Max(lineCount * lineHeight, Math.Max(0, last.Y - first.Y) + lineHeight) + S(12);
        }

        private void ArrangeControls()
        {
            if (!hasLayout) return;
            int margin = S(32), top = S(104), footer = S(88);
            content.Bounds = new Rectangle(margin, top, Math.Max(1, ClientSize.Width - margin * 2),
                Math.Max(1, ClientSize.Height - top - footer));
            int buttonY = ClientSize.Height - S(62);
            copy.Bounds = new Rectangle(ClientSize.Width - margin - S(128), buttonY, S(128), S(40));
            close.Bounds = new Rectangle(copy.Left - S(114), buttonY, S(104), S(40));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!hasLayout || disposing) return;
            ArrangeControls();
            if (!fitting && content.IsHandleCreated) textHeight = MeasureContentHeight();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (titleFont == null) return;
            // Opaque full-surface fill prevents edge artifacts from layered or
            // antialiased backgrounds; only the small brand mark is antialiased.
            e.Graphics.Clear(Paper);
            e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            Branding.DrawMark(e.Graphics, new RectangleF(S(32), S(27), S(32), S(32)));
            DrawText(e.Graphics, "CODEX  /  英文 → 中文", captionFont,
                new Rectangle(S(76), S(23), ClientSize.Width - S(108), S(22)), Muted, false);
            DrawText(e.Graphics, "中文译文", titleFont,
                new Rectangle(S(76), S(46), ClientSize.Width - S(108), S(40)), Ink, false);
            int ruleY = ClientSize.Height - S(88);
            using (var pen = new Pen(Rule)) e.Graphics.DrawLine(pen, S(32), ruleY, ClientSize.Width - S(32), ruleY);
            string status = copied ? "已复制到剪贴板" : "剪贴板新内容已保留";
            DrawText(e.Graphics, status, captionFont,
                new Rectangle(S(32), ruleY + S(22), Math.Max(1, close.Left - S(44)), S(23)), Muted, false);
        }

        private static void DrawText(Graphics graphics, string text, Font font, Rectangle rectangle, Color color, bool centered)
        {
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.FormatFlags |= StringFormatFlags.NoWrap;
                format.Alignment = centered ? StringAlignment.Center : StringAlignment.Near;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisCharacter;
                graphics.DrawString(text, font, brush, rectangle, format);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Hide(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !disposing)
            {
                e.Cancel = true;
                Hide();
            }
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x02E0 && !disposing) // WM_DPICHANGED
            {
                float scale = (m.WParam.ToInt64() & 0xffff) / 96f;
                NativeRectangle suggested = (NativeRectangle)Marshal.PtrToStructure(m.LParam, typeof(NativeRectangle));
                ApplyScale(scale);
                Bounds = new Rectangle(suggested.Left, suggested.Top,
                    suggested.Right - suggested.Left, suggested.Bottom - suggested.Top);
                return;
            }
            base.WndProc(ref m);
        }

        // RichTextBox.DrawToBitmap does not support text rendering. The preview
        // therefore paints the same complete content and fonts directly. Native
        // RichEdit layout and interaction are verified separately by UI tests.
        // Every preview is rasterized afresh rather than enlarging a bitmap.
        public void RenderPreview(string path, float scale)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            if (scale <= 0 || scale > 4) throw new ArgumentOutOfRangeException("scale");
            using (var preview = new TranslationReader())
            {
                preview.ApplyScale(scale);
                preview.fullText = fullText;
                preview.copied = copied;
                preview.content.Text = fullText;
                preview.copy.Enabled = fullText.Length != 0;
                preview.ClientSize = new Size((int)Math.Round(ClientSize.Width / uiScale * scale),
                    (int)Math.Round(ClientSize.Height / uiScale * scale));
                preview.ArrangeControls();
                using (var bitmap = new Bitmap(preview.ClientSize.Width, preview.ClientSize.Height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        preview.OnPaint(new PaintEventArgs(graphics, preview.ClientRectangle));
                        Rectangle body = preview.content.Bounds;
                        bool scroll = UsesVerticalScroll;
                        if (scroll) body.Width -= preview.S(18);
                        using (var brush = new SolidBrush(Ink))
                        using (var format = new StringFormat(StringFormat.GenericTypographic))
                        {
                            // Clip the body only when this is a scrollable view;
                            // fullText remains unchanged in both live and preview.
                            var state = graphics.Save();
                            graphics.SetClip(body);
                            graphics.DrawString(fullText, preview.readerFont, brush, body, format);
                            graphics.Restore(state);
                        }
                        if (scroll)
                            using (var track = new SolidBrush(Rule))
                            using (var thumb = new SolidBrush(Color.FromArgb(168, 181, 175)))
                            {
                                Rectangle bar = new Rectangle(preview.content.Right - preview.S(8), body.Top, preview.S(4), body.Height);
                                graphics.FillRectangle(track, bar);
                                bar.Height = Math.Max(preview.S(24), Math.Min(bar.Height, (int)(bar.Height * content.Height / (float)Math.Max(1, textHeight))));
                                graphics.FillRectangle(thumb, bar);
                            }
                        using (var fill = new SolidBrush(Teal)) graphics.FillRectangle(fill, preview.copy.Bounds);
                        DrawText(graphics, preview.copy.Text, preview.buttonFont, preview.copy.Bounds, Color.White, true);
                        DrawText(graphics, preview.close.Text, preview.buttonFont, preview.close.Bounds, Muted, true);
                    }
                    bitmap.Save(path, ImageFormat.Png);
                }
            }
        }

        protected override void Dispose(bool managed)
        {
            disposing = true;
            base.Dispose(managed);
            if (managed)
            {
                if (readerFont != null) { readerFont.Dispose(); readerFont = null; }
                if (titleFont != null) { titleFont.Dispose(); titleFont = null; }
                if (captionFont != null) { captionFont.Dispose(); captionFont = null; }
                if (buttonFont != null) { buttonFont.Dispose(); buttonFont = null; }
                if (readerIcon != null) { readerIcon.Dispose(); readerIcon = null; }
            }
        }

        private static float WindowScale(IntPtr window)
        {
            try { uint dpi = GetDpiForWindow(window); if (dpi > 0) return dpi / 96f; }
            catch (EntryPointNotFoundException) { }
            using (Graphics graphics = Graphics.FromHwnd(window)) return graphics.DpiX / 96f;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRectangle { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wparam, IntPtr lparam);
    }
}
