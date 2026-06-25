using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AestikModLoader.App
{
    public class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    public sealed class BufferedFlowLayoutPanel : FlowLayoutPanel
    {
        public BufferedFlowLayoutPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    public sealed class ToggleSwitch : Control
    {
        private bool isChecked;

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(52, 28);
        }

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked != value)
                {
                    isChecked = value;
                    Invalidate();
                    if (CheckedChanged != null)
                    {
                        CheckedChanged(this, EventArgs.Empty);
                    }
                }
            }
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 3, Width - 1, Height - 7);
            Color trackColor = Checked ? Color.FromArgb(98, 150, 255) : Color.FromArgb(47, 58, 79);
            using (SolidBrush brush = new SolidBrush(trackColor))
            {
                using (GraphicsPath path = RoundRect(track, track.Height))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }

            int diameter = Height - 8;
            int x = Checked ? Width - diameter - 4 : 4;
            Rectangle thumb = new Rectangle(x, 4, diameter, diameter);
            using (SolidBrush brush = new SolidBrush(Color.White))
            {
                e.Graphics.FillEllipse(brush, thumb);
            }
        }

        private GraphicsPath RoundRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public static class UiStyle
    {
        public static readonly Color Background = Color.FromArgb(11, 16, 32);
        public static readonly Color Surface = Color.FromArgb(17, 24, 40);
        public static readonly Color SurfaceAlt = Color.FromArgb(21, 31, 53);
        public static readonly Color Border = Color.FromArgb(42, 55, 84);
        public static readonly Color Accent = Color.FromArgb(103, 156, 255);
        public static readonly Color AccentSoft = Color.FromArgb(48, 74, 128);
        public static readonly Color Text = Color.FromArgb(239, 244, 255);
        public static readonly Color SubText = Color.FromArgb(162, 176, 204);
        public static readonly Color Danger = Color.FromArgb(255, 102, 102);
        public static readonly Color Success = Color.FromArgb(96, 211, 155);
    }

    public static class UiFactory
    {
        public static Button CreateTabButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.ForeColor = UiStyle.SubText;
            button.BackColor = UiStyle.Background;
            button.Height = 36;
            button.Width = 112;
            button.Margin = new Padding(0, 0, 6, 0);
            button.Font = new Font("Segoe UI Semibold", 15f, FontStyle.Regular);
            button.Cursor = Cursors.Hand;
            return button;
        }

        public static Button CreateActionButton(string text, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.Height = 34;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.ForeColor = Color.White;
            button.BackColor = primary ? UiStyle.Accent : UiStyle.SurfaceAlt;
            button.Padding = new Padding(14, 0, 14, 0);
            return button;
        }

        public static TextBox CreateSearchBox()
        {
            TextBox box = new TextBox();
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = UiStyle.SurfaceAlt;
            box.ForeColor = UiStyle.Text;
            box.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            return box;
        }

        public static Label CreateCaption(string text)
        {
            Label label = new Label();
            label.AutoSize = true;
            label.Text = text;
            label.ForeColor = UiStyle.SubText;
            label.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            return label;
        }
    }
}
