using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MacLookLike
{
    /// <summary>
    /// A small per-pixel-alpha popup that is OWNED by the target window (so it always sits
    /// directly above it in z-order and is hidden when the target is minimized) and draws the
    /// three macOS "traffic light" buttons.
    /// </summary>
    internal sealed class Overlay : Form
    {
        public const int BtnClose = 0, BtnMin = 1, BtnMax = 2;

        public readonly IntPtr Target;
        public event Action<IntPtr, int> ButtonClicked;

        const float Dia = 12f, Gap = 8f, Pad = 13f;

        static readonly Color[] Fills = { Color.FromArgb(255, 95, 87), Color.FromArgb(254, 188, 46), Color.FromArgb(40, 200, 64) };
        static readonly Color[] Edges = { Color.FromArgb(226, 70, 63), Color.FromArgb(225, 161, 22), Color.FromArgb(29, 173, 43) };
        static readonly Color[] Glyphs = { Color.FromArgb(76, 1, 2), Color.FromArgb(152, 87, 18), Color.FromArgb(10, 99, 12) };
        static readonly Color GreyFill = Color.FromArgb(200, 200, 200);
        static readonly Color GreyEdge = Color.FromArgb(172, 172, 172);

        float scale = 1f;
        bool active, canMin, canMax, zoomed;
        bool groupHover, shown, ownerSet;
        int pressed = -1;
        int px, py, pw, ph;

        public Overlay(IntPtr target)
        {
            Target = target;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style = unchecked((int)N.WS_POPUP);
                cp.ExStyle = (int)(N.WS_EX_LAYERED | N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE);
                cp.Parent = Target; // owner
                cp.X = 0; cp.Y = 0; cp.Width = 10; cp.Height = 10;
                return cp;
            }
        }

        public static int GroupWidth(float s) { return (int)Math.Ceiling((Pad * 2 + Dia * 3 + Gap * 2) * s); }
        public static int GroupHeight(float s) { return (int)Math.Ceiling((Dia + Gap) * s); }

        RectangleF CircleRect(int i)
        {
            float d = Dia * scale;
            float x = (Pad + i * (Dia + Gap)) * scale;
            return new RectangleF(x, (ph - d) / 2f, d, d);
        }

        bool IsEnabled(int i) { return i == BtnClose || (i == BtnMin && canMin) || (i == BtnMax && canMax); }

        int HitTest(Point p)
        {
            for (int i = 0; i < 3; i++)
            {
                RectangleF r = CircleRect(i);
                r.Inflate(Gap * 0.5f * scale, Gap * 0.5f * scale);
                if (r.Contains(p.X, p.Y)) return i;
            }
            return -1;
        }

        // ---- public API -----------------------------------------------------
        public void Apply(int x, int titleTop, int titleH, float s, bool act, bool cMin, bool cMax, bool zm, bool vis)
        {
            if (!vis)
            {
                if (shown && IsHandleCreated) { N.ShowWindow(Handle, N.SW_HIDE); }
                shown = false;
                return;
            }
            int w = GroupWidth(s), h = GroupHeight(s);
            int y = titleTop + (titleH - h) / 2;
            bool changed = !shown || x != px || y != py || w != pw || h != ph || s != scale ||
                           act != active || cMin != canMin || cMax != canMax || zm != zoomed;
            if (!changed) return;

            px = x; py = y; pw = w; ph = h;
            scale = s; active = act; canMin = cMin; canMax = cMax; zoomed = zm;

            IntPtr hnd = Handle; // creates the window on first use
            if (!ownerSet)
            {
                // WinForms re-parents non-taskbar forms to its hidden owner; force the real owner.
                N.SetWindowLongPtr(hnd, N.GWLP_HWNDPARENT, Target);
                ownerSet = true;
            }
            Render(hnd);
            if (!shown)
            {
                N.ShowWindow(hnd, N.SW_SHOWNOACTIVATE);
                shown = true;
            }
        }

        // ---- rendering ------------------------------------------------------
        void Render(IntPtr hnd)
        {
            if (pw <= 0 || ph <= 0) return;

            N.BITMAPINFOHEADER bi = new N.BITMAPINFOHEADER();
            bi.biSize = Marshal.SizeOf(typeof(N.BITMAPINFOHEADER));
            bi.biWidth = pw;
            bi.biHeight = -ph; // top-down
            bi.biPlanes = 1;
            bi.biBitCount = 32;
            bi.biCompression = 0;

            IntPtr bits;
            IntPtr dib = N.CreateDIBSection(IntPtr.Zero, ref bi, 0, out bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero) return;
            IntPtr screen = N.GetDC(IntPtr.Zero);
            IntPtr mem = N.CreateCompatibleDC(screen);
            IntPtr old = N.SelectObject(mem, dib);
            try
            {
                using (Bitmap bmp = new Bitmap(pw, ph, pw * 4, PixelFormat.Format32bppPArgb, bits))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    // alpha=1 keeps the area hit-testable (alpha 0 would be click-through) yet invisible
                    g.Clear(Color.FromArgb(1, 0, 0, 0));
                    Draw(g);
                }

                N.POINT dst = new N.POINT(); dst.X = px; dst.Y = py;
                N.SIZE size = new N.SIZE(); size.CX = pw; size.CY = ph;
                N.POINT src = new N.POINT();
                N.BLENDFUNCTION bf = new N.BLENDFUNCTION();
                bf.BlendOp = 0; bf.BlendFlags = 0; bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
                if (!N.UpdateLayeredWindow(hnd, screen, ref dst, ref size, mem, ref src, 0, ref bf, 2))
                    N.Log("UpdateLayeredWindow failed err=" + Marshal.GetLastWin32Error());
            }
            finally
            {
                N.SelectObject(mem, old);
                N.DeleteDC(mem);
                N.ReleaseDC(IntPtr.Zero, screen);
                N.DeleteObject(dib);
            }
        }

        static Color Darken(Color c, float f)
        {
            return Color.FromArgb(c.A, (int)(c.R * f), (int)(c.G * f), (int)(c.B * f));
        }

        void Draw(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < 3; i++)
            {
                RectangleF r = CircleRect(i);
                bool enabled = IsEnabled(i);
                bool colored = enabled && (active || groupHover);
                Color fill = colored ? Fills[i] : GreyFill;
                Color edge = colored ? Edges[i] : GreyEdge;
                if (colored && pressed == i) { fill = Darken(fill, 0.78f); edge = Darken(edge, 0.78f); }

                using (SolidBrush b = new SolidBrush(fill)) g.FillEllipse(b, r);
                RectangleF er = RectangleF.Inflate(r, -0.5f, -0.5f);
                using (Pen p = new Pen(edge, 1f)) g.DrawEllipse(p, er.X, er.Y, er.Width, er.Height);

                if (groupHover && enabled) DrawGlyph(g, i, r);
            }
        }

        void DrawGlyph(Graphics g, int i, RectangleF r)
        {
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, u = scale;
            Color c = Glyphs[i];
            if (pressed == i) c = Darken(c, 0.8f);
            if (i == BtnClose)
            {
                float k = 2.9f * u;
                using (Pen p = new Pen(c, 1.35f * u))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawLine(p, cx - k, cy - k, cx + k, cy + k);
                    g.DrawLine(p, cx - k, cy + k, cx + k, cy - k);
                }
            }
            else if (i == BtnMin)
            {
                float k = 3.3f * u;
                using (Pen p = new Pen(c, 1.35f * u))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawLine(p, cx - k, cy, cx + k, cy);
                }
            }
            else
            {
                PointF[] a, b;
                if (!zoomed)
                {
                    // two triangles whose right angles point outward (enter full-size)
                    a = new PointF[] { new PointF(cx - 0.9f * u, cy - 3.4f * u), new PointF(cx + 3.4f * u, cy - 3.4f * u), new PointF(cx + 3.4f * u, cy + 0.9f * u) };
                    b = new PointF[] { new PointF(cx + 0.9f * u, cy + 3.4f * u), new PointF(cx - 3.4f * u, cy + 3.4f * u), new PointF(cx - 3.4f * u, cy - 0.9f * u) };
                }
                else
                {
                    // right angles pointing to the centre (exit full-size)
                    a = new PointF[] { new PointF(cx + 3.4f * u, cy - 0.6f * u), new PointF(cx + 0.6f * u, cy - 0.6f * u), new PointF(cx + 0.6f * u, cy - 3.4f * u) };
                    b = new PointF[] { new PointF(cx - 3.4f * u, cy + 0.6f * u), new PointF(cx - 0.6f * u, cy + 0.6f * u), new PointF(cx - 0.6f * u, cy + 3.4f * u) };
                }
                using (SolidBrush br = new SolidBrush(c))
                {
                    g.FillPolygon(br, a);
                    g.FillPolygon(br, b);
                }
            }
        }

        void Redraw()
        {
            if (shown && IsHandleCreated) Render(Handle);
        }

        // ---- mouse ----------------------------------------------------------
        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEACTIVATE = 0x0021;
            const int WM_MOUSELEAVE = 0x02A3;
            if (m.Msg == WM_MOUSEACTIVATE) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE
            if (m.Msg == WM_MOUSELEAVE)
            {
                if (groupHover) { groupHover = false; Redraw(); }
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!groupHover)
            {
                groupHover = true;
                N.TRACKMOUSEEVENT t = new N.TRACKMOUSEEVENT();
                t.cbSize = Marshal.SizeOf(typeof(N.TRACKMOUSEEVENT));
                t.dwFlags = 2; // TME_LEAVE
                t.hwndTrack = Handle;
                N.TrackMouseEvent(ref t);
                Redraw();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            int hit = HitTest(e.Location);
            if (hit >= 0 && IsEnabled(hit))
            {
                pressed = hit;
                Capture = true;
                Redraw();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || pressed < 0) return;
            int p = pressed;
            pressed = -1;
            Capture = false;
            bool inside = ClientRectangle.Contains(e.Location);
            if (!inside) groupHover = false;
            Redraw();
            if (inside && HitTest(e.Location) == p)
            {
                Action<IntPtr, int> h = ButtonClicked;
                if (h != null) h(Target, p);
            }
        }
    }
}
