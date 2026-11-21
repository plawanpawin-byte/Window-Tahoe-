using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

static class Theme
{
    public static bool Dark = true;
    public static Color TintColor;
    public static Brush Fg, Sub, Faint, TintBrush, EdgeBrush, GlossBrush, Divider, Pane, CardBg, SideBg, Track;
    public static readonly Color Red = Color.FromRgb(255, 69, 58), Orange = Color.FromRgb(255, 159, 10),
        Green = Color.FromRgb(48, 209, 88), Blue = Color.FromRgb(10, 132, 255), Yellow = Color.FromRgb(255, 214, 10);

    public static Brush B(byte a, byte r, byte g, byte b)
    {
        SolidColorBrush s = new SolidColorBrush(Color.FromArgb(a, r, g, b)); s.Freeze(); return s;
    }
    public static Brush B(Color c) { SolidColorBrush s = new SolidColorBrush(c); s.Freeze(); return s; }

    public static void Apply()
    {
        AppCfg c = Cfg.Data;
        double s = Math.Max(0, Math.Min(1, c.Strength));
        if (c.Tint == 1)
        {
            Dark = false;
            TintColor = Color.FromArgb((byte)(40 + s * 190), 248, 248, 252);
            Fg = B(255, 28, 28, 30); Sub = B(170, 28, 28, 30); Faint = B(90, 28, 28, 30);
            Divider = B(34, 0, 0, 0); Track = B(40, 0, 0, 0);
        }
        else
        {
            Dark = true;
            if (c.Tint == 2) TintColor = Color.FromArgb((byte)(6 + s * 60), 255, 255, 255);
            else TintColor = Color.FromArgb((byte)(40 + s * 190), 22, 22, 26);
            Fg = B(255, 255, 255, 255); Sub = B(185, 255, 255, 255); Faint = B(100, 255, 255, 255);
            Divider = B(36, 255, 255, 255); Track = B(46, 255, 255, 255);
        }
        TintBrush = B(TintColor);
        Pane = Dark ? B(255, 30, 30, 33) : B(255, 242, 242, 247);
        CardBg = Dark ? B(255, 44, 44, 48) : B(255, 255, 255, 255);
        SideBg = Dark ? B(150, 32, 32, 36) : B(170, 236, 236, 242);

        LinearGradientBrush e = new LinearGradientBrush();
        e.StartPoint = new Point(0, 0); e.EndPoint = new Point(1, 1);
        e.GradientStops.Add(new GradientStop(Color.FromArgb(140, 255, 255, 255), 0));
        e.GradientStops.Add(new GradientStop(Color.FromArgb(24, 255, 255, 255), 0.5));
        e.GradientStops.Add(new GradientStop(Color.FromArgb(70, 255, 255, 255), 1));
        e.Freeze(); EdgeBrush = e;

        LinearGradientBrush g = new LinearGradientBrush();
        g.StartPoint = new Point(0.2, 0); g.EndPoint = new Point(0.8, 1);
        g.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(Dark ? 46 : 90), 255, 255, 255), 0));
        g.GradientStops.Add(new GradientStop(Color.FromArgb(10, 255, 255, 255), 0.5));
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
        g.Freeze(); GlossBrush = g;
    }
}

static class U
{
    static readonly FontFamily Font = new FontFamily("Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily Icons = new FontFamily("Segoe MDL2 Assets");

    public static TextBlock T(string s, double size, FontWeight w, Brush b)
    {
        TextBlock t = new TextBlock();
        t.Text = s; t.FontSize = size; t.FontWeight = w; t.Foreground = b; t.FontFamily = Font;
        return t;
    }

    // Analog clock face drawn on a size x size canvas.
    public static Canvas Analog(double size, DateTime n, bool seconds)
    {
        Canvas c = new Canvas(); c.Width = size; c.Height = size;
        double r = size / 2, cx = r, cy = r;
        Ellipse face = new Ellipse(); face.Width = size - 2; face.Height = size - 2;
        Canvas.SetLeft(face, 1); Canvas.SetTop(face, 1);
        face.Stroke = Theme.Faint; face.StrokeThickness = 1.5;
        face.Fill = Theme.Dark ? Theme.B(30, 255, 255, 255) : Theme.B(60, 255, 255, 255);
        c.Children.Add(face);
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6;
            double len = i % 3 == 0 ? r * 0.12 : r * 0.06;
            Line l = new Line();
            l.X1 = cx + Math.Sin(a) * (r * 0.9); l.Y1 = cy - Math.Cos(a) * (r * 0.9);
            l.X2 = cx + Math.Sin(a) * (r * 0.9 - len); l.Y2 = cy - Math.Cos(a) * (r * 0.9 - len);
            l.Stroke = i % 3 == 0 ? Theme.Fg : Theme.Sub; l.StrokeThickness = i % 3 == 0 ? 2 : 1;
            c.Children.Add(l);
        }
        double h = ((n.Hour % 12) + n.Minute / 60.0) * 30, m = (n.Minute + n.Second / 60.0) * 6, s = n.Second * 6;
        c.Children.Add(Hand(cx, cy, h, r * 0.5, Math.Max(3, size / 28), Theme.Fg));
        c.Children.Add(Hand(cx, cy, m, r * 0.76, Math.Max(2.5, size / 40), Theme.Fg));
        if (seconds) c.Children.Add(Hand(cx, cy, s, r * 0.84, 1.5, Theme.B(Theme.Orange)));
        Ellipse dot = new Ellipse(); dot.Width = 7; dot.Height = 7; dot.Fill = seconds ? Theme.B(Theme.Orange) : Theme.Fg;
        Canvas.SetLeft(dot, cx - 3.5); Canvas.SetTop(dot, cy - 3.5);
        c.Children.Add(dot);
        return c;
    }

    static Line Hand(double cx, double cy, double deg, double len, double th, Brush b)
    {
        double a = deg * Math.PI / 180;
        Line l = new Line();
        l.X1 = cx; l.Y1 = cy; l.X2 = cx + Math.Sin(a) * len; l.Y2 = cy - Math.Cos(a) * len;
        l.Stroke = b; l.StrokeThickness = th; l.StrokeStartLineCap = PenLineCap.Round; l.StrokeEndLineCap = PenLineCap.Round;
        return l;
    }
}

// Borderless, see-through "liquid glass" window that lives on the desktop layer.
class GlassWindow : Window
{
    protected double Radius = 22;
    protected bool Pinned = true;
    protected bool NoActivate = true;
    IntPtr hwnd, host;
    HwndSource src;
    DispatcherTimer reassert;

    public GlassWindow()
    {
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Background = Brushes.Transparent; WindowStartupLocation = WindowStartupLocation.Manual;
        ShowActivated = false; SnapsToDevicePixels = true; UseLayoutRounding = true;
        SizeChanged += delegate { ApplyRegion(); };
    }

    protected double Scale()
    {
        return src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M11 : 1.0;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        src = (HwndSource)PresentationSource.FromVisual(this);
        hwnd = src.Handle;
        src.CompositionTarget.BackgroundColor = Colors.Transparent;
        Native.ExtendFrame(hwnd);
        Native.EnableBlur(hwnd);
        int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        ex |= Native.WS_EX_TOOLWINDOW;
        if (NoActivate) ex |= Native.WS_EX_NOACTIVATE;
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex);
        if (Pinned)
        {
            host = Native.DesktopHost();
            src.AddHook(Hook);
            reassert = new DispatcherTimer();
            reassert.Interval = TimeSpan.FromSeconds(3);
            reassert.Tick += delegate
            {
                if (host == IntPtr.Zero || !Native.IsWindow(host)) host = Native.DesktopHost();
                Pin();
            };
            reassert.Start();
            Pin();
        }
        ApplyRegion();
    }

    void Pin()
    {
        if (hwnd == IntPtr.Zero) return;
        IntPtr after = host != IntPtr.Zero ? host : Native.HWND_BOTTOM;
        Native.SetWindowPos(hwnd, after, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == Native.WM_WINDOWPOSCHANGING && host != IntPtr.Zero)
        {
            Native.WINDOWPOS p = (Native.WINDOWPOS)Marshal.PtrToStructure(l, typeof(Native.WINDOWPOS));
            if ((p.flags & Native.SWP_NOZORDER) == 0)
            {
                p.hwndInsertAfter = host;
                Marshal.StructureToPtr(p, l, false);
            }
        }
        return IntPtr.Zero;
    }

    protected void ApplyRegion()
    {
        if (hwnd == IntPtr.Zero || Radius <= 0) return;
        double sc = Scale();
        int w = (int)Math.Round(ActualWidth * sc), h = (int)Math.Round(ActualHeight * sc);
        if (w <= 0 || h <= 0) return;
        int d = (int)(Radius * 2 * sc);
        Native.SetWindowRgn(hwnd, Native.CreateRoundRectRgn(0, 0, w + 1, h + 1, d, d), true);
    }
}
