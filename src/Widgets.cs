using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

enum Mode { S, M, L }

class WContent
{
    public FrameworkElement Root;
    public Action Update;
    public bool Fluid;
    public FrameworkElement NoDrag;
    public Color? Tint;
}

// Widget factory: every type has two styles and three sizes (S / M / L).
static class WF
{
    public static readonly string[] Types = { "Clock", "Calendar", "Weather", "Battery", "Note" };
    static readonly CultureInfo E = new CultureInfo("en-US");
    static readonly FontWeight Nm = FontWeights.Normal, Sb = FontWeights.SemiBold, Bd = FontWeights.Bold, Lt = FontWeights.Light;
    static readonly FontFamily EmojiFont = new FontFamily("Segoe UI Emoji");

    public static string StyleName(string type, int s)
    {
        switch (type)
        {
            case "Clock": return s == 0 ? "Digital" : "Analog";
            case "Calendar": return s == 0 ? "Date" : "Month";
            case "Weather": return s == 0 ? "Details" : "Forecast";
            case "Battery": return s == 0 ? "Rings" : "Bars";
            default: return s == 0 ? "Sticky" : "Glass";
        }
    }

    public static Size Dim(Mode m)
    {
        switch (m)
        {
            case Mode.S: return new Size(158, 158);
            case Mode.M: return new Size(338, 158);
            default: return new Size(338, 338);
        }
    }
    public static Size Inner(Mode m) { Size d = Dim(m); return new Size(d.Width - 28, d.Height - 28); }
    public static Mode ModeFor(double w, double h)
    {
        if (h >= 250) return Mode.L;
        if (w >= 250) return Mode.M;
        return Mode.S;
    }

    public static WContent Build(WidgetCfg c, Mode m) { return BuildFor(c.Type, c.Style, m, c); }

    public static WContent BuildFor(string type, int style, Mode m, WidgetCfg cfg)
    {
        switch (type)
        {
            case "Clock": return ClockW(style, m);
            case "Calendar": return CalendarW(style, m);
            case "Weather": return WeatherW(style, m);
            case "Battery": return BatteryW(style, m);
            default: return NoteW(style, m, cfg);
        }
    }

    // glass tile around the content
    public static FrameworkElement Face(WContent c)
    {
        Grid g = new Grid();
        Border tint = new Border();
        tint.CornerRadius = new CornerRadius(22);
        tint.Background = c.Tint.HasValue ? Theme.B(c.Tint.Value) : Theme.TintBrush;
        Border gloss = new Border();
        gloss.CornerRadius = new CornerRadius(22); gloss.Background = Theme.GlossBrush; gloss.IsHitTestVisible = false;
        Grid body = new Grid(); body.Margin = new Thickness(14);
        if (c.Fluid) body.Children.Add(c.Root);
        else
        {
            Viewbox vb = new Viewbox(); vb.Stretch = Stretch.Uniform; vb.Child = c.Root;
            body.Children.Add(vb);
        }
        Border edge = new Border();
        edge.CornerRadius = new CornerRadius(22); edge.BorderThickness = new Thickness(1);
        edge.BorderBrush = Theme.EdgeBrush; edge.IsHitTestVisible = false;
        g.Children.Add(tint); g.Children.Add(gloss); g.Children.Add(body); g.Children.Add(edge);
        return g;
    }

    // static, scaled-down sample used in the "Desktop Widgets" gallery
    public static FrameworkElement Preview(string type, int style, Mode m, double scale)
    {
        WContent c = BuildFor(type, style, m, null);
        Size d = Dim(m);
        Grid inner = new Grid(); inner.Width = d.Width; inner.Height = d.Height;
        inner.Children.Add(Face(c));
        Viewbox vb = new Viewbox(); vb.Width = d.Width * scale; vb.Height = d.Height * scale;
        vb.Stretch = Stretch.Uniform; vb.Child = inner; vb.IsHitTestVisible = false;
        return vb;
    }

    // ---------- helpers ----------
    static TextBlock T(string s, double size, FontWeight w, Brush b) { return U.T(s, size, w, b); }
    static T Ctr<T>(T e) where T : FrameworkElement { e.HorizontalAlignment = HorizontalAlignment.Center; return e; }
    static T Cell<T>(T e, int r, int c) where T : UIElement { Grid.SetRow(e, r); Grid.SetColumn(e, c); return e; }

    static WContent Dyn(Mode m, Func<string> sig, Func<UIElement> build)
    {
        Size s = Inner(m);
        Border host = new Border(); host.Width = s.Width; host.Height = s.Height;
        string last = null;
        Action upd = delegate
        {
            string g = sig();
            if (g != last) { last = g; host.Child = build(); }
        };
        WContent c = new WContent(); c.Root = host; c.Update = upd;
        upd();
        return c;
    }

    static UIElement TimeBlock(DateTime n, double size, FontWeight w, Brush b)
    {
        bool h24 = Cfg.Data.Hour24;
        StackPanel p = new StackPanel(); p.Orientation = Orientation.Horizontal; p.HorizontalAlignment = HorizontalAlignment.Center;
        p.Children.Add(T(n.ToString(h24 ? "HH:mm" : "h:mm", E), size, w, b));
        if (!h24)
        {
            TextBlock a = T(n.ToString("tt", E), size * 0.3, Sb, Theme.Sub);
            a.VerticalAlignment = VerticalAlignment.Bottom; a.Margin = new Thickness(3, 0, 0, size * 0.14);
            p.Children.Add(a);
        }
        return p;
    }

    // ================= CLOCK =================
    static WContent ClockW(int style, Mode m)
    {
        bool analog = style == 1;
        return Dyn(m, delegate
        {
            DateTime n = DateTime.Now;
            return n.ToString(analog ? "HHmmss" : "HHmm", E) + n.ToString("yyyyMMdd", E) + Cfg.Data.Hour24;
        }, delegate
        {
            return analog ? ClockAnalog(m, DateTime.Now) : ClockDigital(m, DateTime.Now);
        });
    }

    static UIElement ClockDigital(Mode m, DateTime n)
    {
        StackPanel p = new StackPanel();
        p.VerticalAlignment = VerticalAlignment.Center; p.HorizontalAlignment = HorizontalAlignment.Center;
        if (m == Mode.S)
        {
            p.Children.Add(Ctr(T(n.ToString("ddd", E).ToUpper(E), 13, Bd, Theme.B(Theme.Orange))));
            p.Children.Add(TimeBlock(n, 40, Lt, Theme.Fg));
            p.Children.Add(Ctr(T(n.ToString("d MMM", E), 14, Nm, Theme.Sub)));
        }
        else if (m == Mode.M)
        {
            p.Children.Add(Ctr(T(n.ToString("dddd", E).ToUpper(E), 14, Bd, Theme.B(Theme.Orange))));
            p.Children.Add(TimeBlock(n, 64, Lt, Theme.Fg));
            p.Children.Add(Ctr(T(n.ToString("d MMMM yyyy", E), 16, Nm, Theme.Sub)));
        }
        else
        {
            p.Children.Add(Ctr(T(n.ToString("dddd", E).ToUpper(E), 16, Bd, Theme.B(Theme.Orange))));
            p.Children.Add(TimeBlock(n, 84, Lt, Theme.Fg));
            p.Children.Add(Ctr(T(n.ToString("d MMMM yyyy", E), 20, Nm, Theme.Sub)));
            Border line = new Border(); line.Height = 1; line.Background = Theme.Divider; line.Margin = new Thickness(0, 18, 0, 14);
            p.Children.Add(line);
            int week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(n, CalendarWeekRule.FirstDay, DayOfWeek.Sunday);
            int total = DateTime.IsLeapYear(n.Year) ? 366 : 365;
            Grid st = new Grid(); st.Width = 290;
            for (int i = 0; i < 3; i++) st.ColumnDefinitions.Add(new ColumnDefinition());
            string[] num = { week.ToString(), n.DayOfYear.ToString(), (total - n.DayOfYear).ToString() };
            string[] cap = { "Week", "Day of year", "Days left" };
            for (int i = 0; i < 3; i++)
            {
                StackPanel s = new StackPanel();
                s.Children.Add(Ctr(T(num[i], 26, Sb, Theme.Fg)));
                s.Children.Add(Ctr(T(cap[i], 11, Nm, Theme.Sub)));
                st.Children.Add(Cell(s, 0, i));
            }
            p.Children.Add(st);
        }
        return p;
    }

    static UIElement ClockAnalog(Mode m, DateTime n)
    {
        Grid g = new Grid();
        if (m == Mode.S)
        {
            Canvas a = U.Analog(130, n, true); a.HorizontalAlignment = HorizontalAlignment.Center; a.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(a);
        }
        else if (m == Mode.M)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            Canvas a = U.Analog(130, n, true); a.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(Cell(a, 0, 0));
            StackPanel p = new StackPanel(); p.VerticalAlignment = VerticalAlignment.Center; p.HorizontalAlignment = HorizontalAlignment.Center;
            p.Children.Add(Ctr(T(n.ToString("dddd", E).ToUpper(E), 12, Bd, Theme.B(Theme.Orange))));
            p.Children.Add(TimeBlock(n, 44, Lt, Theme.Fg));
            p.Children.Add(Ctr(T(n.ToString("d MMMM", E), 15, Nm, Theme.Sub)));
            g.Children.Add(Cell(p, 0, 1));
        }
        else
        {
            StackPanel p = new StackPanel(); p.VerticalAlignment = VerticalAlignment.Center; p.HorizontalAlignment = HorizontalAlignment.Center;
            Canvas a = U.Analog(250, n, true); a.HorizontalAlignment = HorizontalAlignment.Center;
            p.Children.Add(a);
            TextBlock d = Ctr(T(n.ToString("dddd, d MMMM", E), 18, Sb, Theme.Fg)); d.Margin = new Thickness(0, 10, 0, 0);
            p.Children.Add(d);
            g.Children.Add(p);
        }
        return g;
    }

    // ================= CALENDAR =================
    static UIElement MonthGrid(DateTime n, double w, double h, double fs)
    {
        DateTime first = new DateTime(n.Year, n.Month, 1);
        int off = (int)first.DayOfWeek, days = DateTime.DaysInMonth(n.Year, n.Month);
        int rows = (off + days + 6) / 7;
        Grid g = new Grid(); g.Width = w; g.Height = h;
        for (int c = 0; c < 7; c++) g.ColumnDefinitions.Add(new ColumnDefinition());
        for (int r = 0; r < rows + 1; r++) g.RowDefinitions.Add(new RowDefinition());
        string[] hd = { "S", "M", "T", "W", "T", "F", "S" };
        for (int c = 0; c < 7; c++)
        {
            TextBlock t = T(hd[c], fs, Sb, Theme.Faint);
            t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(Cell(t, 0, c));
        }
        double dia = Math.Min(w / 7, h / (rows + 1)) - 2;
        for (int d = 1; d <= days; d++)
        {
            int idx = off + d - 1, r = idx / 7 + 1, c = idx % 7;
            bool today = d == n.Day;
            Grid cell = new Grid();
            if (today)
            {
                Ellipse e = new Ellipse(); e.Width = dia; e.Height = dia; e.Fill = Theme.B(Theme.Red);
                cell.Children.Add(e);
            }
            TextBlock t = T(d.ToString(), fs, today ? Sb : Nm, today ? Brushes.White : (c == 0 || c == 6 ? Theme.Sub : Theme.Fg));
            t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center;
            cell.Children.Add(t);
            g.Children.Add(Cell(cell, r, c));
        }
        return g;
    }

    static WContent CalendarW(int style, Mode m)
    {
        return Dyn(m, delegate { return DateTime.Now.ToString("yyyyMMdd", E); }, delegate
        {
            DateTime n = DateTime.Now;
            return style == 0 ? CalDate(m, n) : CalMonth(m, n);
        });
    }

    static UIElement DateBlock(DateTime n, double dayFs, double wdFs, double moFs, bool center)
    {
        StackPanel p = new StackPanel(); p.VerticalAlignment = VerticalAlignment.Center;
        TextBlock a = T(n.ToString("dddd", E).ToUpper(E), wdFs, Bd, Theme.B(Theme.Red));
        TextBlock b = T(n.Day.ToString(), dayFs, Lt, Theme.Fg);
        TextBlock c = T(n.ToString("MMMM yyyy", E), moFs, Nm, Theme.Sub);
        if (center) { Ctr(a); Ctr(b); Ctr(c); p.HorizontalAlignment = HorizontalAlignment.Center; }
        p.Children.Add(a); p.Children.Add(b); p.Children.Add(c);
        return p;
    }

    static UIElement CalDate(Mode m, DateTime n)
    {
        if (m == Mode.S) return DateBlock(n, 64, 13, 13, true);
        Grid g = new Grid();
        if (m == Mode.M)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(Cell(DateBlock(n, 60, 13, 12, false), 0, 0));
            UIElement mg = MonthGrid(n, 175, 126, 10.5);
            ((FrameworkElement)mg).HorizontalAlignment = HorizontalAlignment.Right;
            ((FrameworkElement)mg).VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(Cell(mg, 0, 1));
            return g;
        }
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(105) });
        g.RowDefinitions.Add(new RowDefinition());
        g.Children.Add(Cell(DateBlock(n, 70, 15, 15, false), 0, 0));
        UIElement big = MonthGrid(n, 310, 195, 13);
        ((FrameworkElement)big).VerticalAlignment = VerticalAlignment.Bottom;
        g.Children.Add(Cell(big, 1, 0));
        return g;
    }

    static UIElement CalMonth(Mode m, DateTime n)
    {
        if (m == Mode.S)
        {
            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition());
            g.Children.Add(Cell(Ctr(T(n.ToString("MMMM", E).ToUpper(E), 12, Bd, Theme.B(Theme.Red))), 0, 0));
            UIElement mg = MonthGrid(n, 130, 108, 9.5);
            g.Children.Add(Cell(mg, 1, 0));
            return g;
        }
        if (m == Mode.M)
        {
            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.Children.Add(Cell(T(n.ToString("MMMM yyyy", E).ToUpper(E), 13, Bd, Theme.B(Theme.Red)), 0, 0));
            UniformGrid strip = new UniformGrid(); strip.Columns = 7; strip.VerticalAlignment = VerticalAlignment.Center;
            DateTime sun = n.Date.AddDays(-(int)n.DayOfWeek);
            string[] hd = { "S", "M", "T", "W", "T", "F", "S" };
            for (int i = 0; i < 7; i++)
            {
                DateTime d = sun.AddDays(i);
                bool today = d.Date == n.Date;
                StackPanel col = new StackPanel(); col.HorizontalAlignment = HorizontalAlignment.Center;
                col.Children.Add(Ctr(T(hd[i], 12, Sb, Theme.Faint)));
                Grid dot = new Grid(); dot.Width = 34; dot.Height = 34; dot.Margin = new Thickness(0, 4, 0, 0);
                if (today) { Ellipse e = new Ellipse(); e.Fill = Theme.B(Theme.Red); dot.Children.Add(e); }
                TextBlock t = T(d.Day.ToString(), 17, today ? Sb : Nm, today ? Brushes.White : Theme.Fg);
                t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center;
                dot.Children.Add(t);
                col.Children.Add(dot);
                strip.Children.Add(col);
            }
            g.Children.Add(Cell(strip, 1, 0));
            g.Children.Add(Cell(T(n.ToString("dddd, d MMMM", E), 13, Nm, Theme.Sub), 2, 0));
            return g;
        }
        Grid l = new Grid();
        l.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        l.RowDefinitions.Add(new RowDefinition());
        StackPanel hdp = new StackPanel(); hdp.Orientation = Orientation.Horizontal; hdp.VerticalAlignment = VerticalAlignment.Top;
        hdp.Children.Add(T(n.ToString("MMMM", E), 28, Bd, Theme.B(Theme.Red)));
        TextBlock y = T(n.ToString("yyyy", E), 28, Lt, Theme.Sub); y.Margin = new Thickness(8, 0, 0, 0);
        hdp.Children.Add(y);
        l.Children.Add(Cell(hdp, 0, 0));
        l.Children.Add(Cell(MonthGrid(n, 310, 250, 15), 1, 0));
        return l;
    }

    // ================= WEATHER =================
    static string Tmp(double c)
    {
        double v = Cfg.Data.Fahrenheit ? c * 9 / 5 + 32 : c;
        int r = (int)Math.Round(v);
        return r.ToString(CultureInfo.InvariantCulture) + "°";
    }

    static string Glyph(int code, bool day)
    {
        if (code == 0 || code == 1) return day ? "☀" : "\U0001F319";
        if (code == 2) return day ? "⛅" : "☁";
        if (code == 3) return "☁";
        if (code == 45 || code == 48) return "\U0001F32B";
        if (code >= 51 && code <= 57) return "\U0001F326";
        if (code >= 61 && code <= 67) return "\U0001F327";
        if (code >= 71 && code <= 77) return "\U0001F328";
        if (code >= 80 && code <= 82) return "\U0001F326";
        if (code == 85 || code == 86) return "\U0001F328";
        if (code >= 95) return "⛈";
        return "☁";
    }

    static Brush GlyphBrush(int code, bool day)
    {
        if (code <= 1) return day ? Theme.B(Theme.Yellow) : Theme.B(255, 205, 215, 255);
        if (code == 2) return day ? Theme.B(255, 255, 224, 120) : Theme.Sub;
        if (code >= 95) return Theme.B(Theme.Orange);
        if ((code >= 51 && code <= 67) || (code >= 80 && code <= 82)) return Theme.B(255, 110, 190, 255);
        return Theme.Dark ? Theme.B(255, 235, 240, 250) : Theme.B(255, 90, 100, 120);
    }

    static string Desc(int code)
    {
        if (code == 0) return "Clear";
        if (code == 1) return "Mostly clear";
        if (code == 2) return "Partly cloudy";
        if (code == 3) return "Overcast";
        if (code == 45 || code == 48) return "Fog";
        if (code >= 51 && code <= 57) return "Drizzle";
        if (code >= 61 && code <= 67) return "Rain";
        if (code >= 71 && code <= 77) return "Snow";
        if (code >= 80 && code <= 82) return "Showers";
        if (code == 85 || code == 86) return "Snow showers";
        if (code >= 95) return "Thunderstorm";
        return "Cloudy";
    }

    static TextBlock WIcon(int code, bool day, double size)
    {
        TextBlock t = T(Glyph(code, day), size, Nm, GlyphBrush(code, day));
        t.FontFamily = EmojiFont;
        return t;
    }

    static string HourLabel(string hh)
    {
        int h;
        if (!int.TryParse(hh, out h)) return hh;
        if (Cfg.Data.Hour24) return h.ToString("00");
        return ((h % 12 == 0) ? 12 : h % 12).ToString() + (h >= 12 ? "PM" : "AM");
    }

    static WContent WeatherW(int style, Mode m)
    {
        return Dyn(m, delegate
        {
            WeatherData d = Weather.Data;
            return (d == null ? "-" : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(d).ToString()) + Weather.Status + Cfg.Data.Fahrenheit + Cfg.Data.Hour24;
        }, delegate
        {
            WeatherData d = Weather.Data;
            if (d == null) return WeatherEmpty();
            return style == 0 ? WeatherDetail(d, m) : WeatherForecast(d, m);
        });
    }

    static UIElement WeatherEmpty()
    {
        StackPanel p = new StackPanel(); p.VerticalAlignment = VerticalAlignment.Center; p.HorizontalAlignment = HorizontalAlignment.Center;
        p.Children.Add(Ctr(WIcon(3, true, 34)));
        TextBlock t = Ctr(T(Weather.Status, 14, Sb, Theme.Sub)); t.Margin = new Thickness(0, 6, 0, 0);
        p.Children.Add(t);
        return p;
    }

    static UIElement CurBlock(WeatherData d, double tempFs, double iconFs)
    {
        Grid g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition());
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        TextBlock city = T(string.IsNullOrEmpty(d.City) ? "Weather" : d.City, 14, Sb, Theme.Fg);
        city.TextTrimming = TextTrimming.CharacterEllipsis;
        top.Children.Add(Cell(city, 0, 0));
        top.Children.Add(Cell(WIcon(d.Code, d.IsDay, iconFs), 0, 1));
        g.Children.Add(Cell(top, 0, 0));
        TextBlock temp = T(Tmp(d.Temp), tempFs, Lt, Theme.Fg); temp.VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(Cell(temp, 1, 0));
        StackPanel bot = new StackPanel();
        bot.Children.Add(T(Desc(d.Code), 13, Nm, Theme.Sub));
        if (d.DMax.Count > 0) bot.Children.Add(T("H:" + Tmp(d.DMax[0]) + "  L:" + Tmp(d.DMin[0]), 13, Nm, Theme.Sub));
        g.Children.Add(Cell(bot, 2, 0));
        return g;
    }

    static UIElement HourStrip(WeatherData d, int count)
    {
        UniformGrid u = new UniformGrid(); u.Columns = count;
        for (int i = 0; i < count && i < d.HT.Count; i++)
        {
            StackPanel c = new StackPanel(); c.HorizontalAlignment = HorizontalAlignment.Center; c.VerticalAlignment = VerticalAlignment.Center;
            int h; int.TryParse(d.HL[i], out h);
            c.Children.Add(Ctr(T(HourLabel(d.HL[i]), 11, Sb, Theme.Sub)));
            TextBlock ic = Ctr(WIcon(d.HC[i], h >= 6 && h < 18, 20)); ic.Margin = new Thickness(0, 4, 0, 4);
            c.Children.Add(ic);
            c.Children.Add(Ctr(T(Tmp(d.HT[i]), 14, Sb, Theme.Fg)));
            u.Children.Add(c);
        }
        return u;
    }

    static UIElement DailyRows(WeatherData d, int first, int count, double rowH)
    {
        int n = Math.Min(count, d.DMax.Count - first);
        StackPanel sp = new StackPanel(); sp.VerticalAlignment = VerticalAlignment.Bottom;
        if (n <= 0) return sp;
        double gmin = double.MaxValue, gmax = double.MinValue;
        for (int i = first; i < first + n; i++) { gmin = Math.Min(gmin, d.DMin[i]); gmax = Math.Max(gmax, d.DMax[i]); }
        if (gmax - gmin < 1) gmax = gmin + 1;
        for (int i = first; i < first + n; i++)
        {
            Grid row = new Grid(); row.Height = rowH;
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            TextBlock nm = T(d.DName[i], 14, Sb, Theme.Fg); nm.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(Cell(nm, 0, 0));
            TextBlock ic = WIcon(d.DCode[i], true, 17); ic.VerticalAlignment = VerticalAlignment.Center; ic.HorizontalAlignment = HorizontalAlignment.Center;
            row.Children.Add(Cell(ic, 0, 1));
            TextBlock lo = T(Tmp(d.DMin[i]), 14, Nm, Theme.Sub); lo.VerticalAlignment = VerticalAlignment.Center; lo.TextAlignment = TextAlignment.Right;
            row.Children.Add(Cell(lo, 0, 2));
            Grid bar = new Grid(); bar.VerticalAlignment = VerticalAlignment.Center; bar.Margin = new Thickness(8, 0, 8, 0); bar.Height = 5;
            Border track = new Border(); track.CornerRadius = new CornerRadius(2.5); track.Background = Theme.Track;
            bar.Children.Add(track);
            Grid fillGrid = new Grid();
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.0001, d.DMin[i] - gmin), GridUnitType.Star) });
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.0001, d.DMax[i] - d.DMin[i]), GridUnitType.Star) });
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.0001, gmax - d.DMax[i]), GridUnitType.Star) });
            Border fill = new Border(); fill.CornerRadius = new CornerRadius(2.5);
            LinearGradientBrush gb = new LinearGradientBrush(Color.FromRgb(100, 200, 255), Color.FromRgb(255, 190, 60), 0);
            fill.Background = gb;
            fillGrid.Children.Add(Cell(fill, 0, 1));
            bar.Children.Add(fillGrid);
            row.Children.Add(Cell(bar, 0, 3));
            TextBlock hi = T(Tmp(d.DMax[i]), 14, Sb, Theme.Fg); hi.VerticalAlignment = VerticalAlignment.Center; hi.TextAlignment = TextAlignment.Right;
            row.Children.Add(Cell(hi, 0, 4));
            sp.Children.Add(row);
        }
        return sp;
    }

    static UIElement WeatherDetail(WeatherData d, Mode m)
    {
        if (m == Mode.S) return CurBlock(d, 46, 24);
        Grid g = new Grid();
        if (m == Mode.M)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(Cell(CurBlock(d, 46, 22), 0, 0));
            g.Children.Add(Cell(HourStrip(d, 4), 0, 1));
            return g;
        }
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(108) });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(78) });
        g.RowDefinitions.Add(new RowDefinition());
        g.Children.Add(Cell(CurBlock(d, 54, 30), 0, 0));
        g.Children.Add(Cell(HourStrip(d, 6), 1, 0));
        g.Children.Add(Cell(DailyRows(d, 0, 4, 28), 2, 0));
        return g;
    }

    static UIElement WeatherForecast(WeatherData d, Mode m)
    {
        if (m == Mode.S)
        {
            StackPanel p = new StackPanel(); p.VerticalAlignment = VerticalAlignment.Center; p.HorizontalAlignment = HorizontalAlignment.Center;
            p.Children.Add(Ctr(T(string.IsNullOrEmpty(d.City) ? "Weather" : d.City, 13, Sb, Theme.Sub)));
            p.Children.Add(Ctr(WIcon(d.Code, d.IsDay, 42)));
            p.Children.Add(Ctr(T(Tmp(d.Temp), 34, Lt, Theme.Fg)));
            p.Children.Add(Ctr(T(Desc(d.Code), 13, Nm, Theme.Sub)));
            return p;
        }
        if (m == Mode.M)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            StackPanel l = new StackPanel(); l.VerticalAlignment = VerticalAlignment.Center;
            l.Children.Add(T(string.IsNullOrEmpty(d.City) ? "Weather" : d.City, 13, Sb, Theme.Fg));
            l.Children.Add(T(Tmp(d.Temp), 40, Lt, Theme.Fg));
            l.Children.Add(WIcon(d.Code, d.IsDay, 22));
            g.Children.Add(Cell(l, 0, 0));
            UniformGrid u = new UniformGrid(); u.Columns = 4;
            for (int i = 1; i <= 4 && i < d.DMax.Count; i++)
            {
                StackPanel c = new StackPanel(); c.VerticalAlignment = VerticalAlignment.Center; c.HorizontalAlignment = HorizontalAlignment.Center;
                c.Children.Add(Ctr(T(d.DName[i], 12, Sb, Theme.Sub)));
                TextBlock ic = Ctr(WIcon(d.DCode[i], true, 22)); ic.Margin = new Thickness(0, 5, 0, 5);
                c.Children.Add(ic);
                c.Children.Add(Ctr(T(Tmp(d.DMax[i]), 15, Sb, Theme.Fg)));
                c.Children.Add(Ctr(T(Tmp(d.DMin[i]), 13, Nm, Theme.Faint)));
                u.Children.Add(c);
            }
            g.Children.Add(Cell(u, 0, 1));
            return g;
        }
        Grid lg = new Grid();
        lg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(92) });
        lg.RowDefinitions.Add(new RowDefinition());
        Grid top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        StackPanel tl = new StackPanel(); tl.VerticalAlignment = VerticalAlignment.Center;
        tl.Children.Add(T(string.IsNullOrEmpty(d.City) ? "Weather" : d.City, 20, Sb, Theme.Fg));
        tl.Children.Add(T(Desc(d.Code), 14, Nm, Theme.Sub));
        top.Children.Add(Cell(tl, 0, 0));
        StackPanel tr = new StackPanel(); tr.Orientation = Orientation.Horizontal; tr.VerticalAlignment = VerticalAlignment.Center;
        tr.Children.Add(T(Tmp(d.Temp), 52, Lt, Theme.Fg));
        TextBlock ic2 = WIcon(d.Code, d.IsDay, 34); ic2.Margin = new Thickness(8, 0, 0, 0); ic2.VerticalAlignment = VerticalAlignment.Center;
        tr.Children.Add(ic2);
        top.Children.Add(Cell(tr, 0, 1));
        lg.Children.Add(Cell(top, 0, 0));
        lg.Children.Add(Cell(DailyRows(d, 0, 6, 36), 1, 0));
        return lg;
    }

    // ================= BATTERY =================
    static Brush BatBrush(BatItem it)
    {
        if (it.Charging) return Theme.B(Theme.Green);
        if (it.Percent <= 20) return Theme.B(Theme.Red);
        return Theme.B(Theme.Green);
    }

    static string BatName(BatItem it) { return (it.Charging ? "⚡ " : "") + it.Name; }

    static UIElement Ring(BatItem it, double d, double thick, double fs)
    {
        Grid g = new Grid(); g.Width = d; g.Height = d;
        Ellipse tr = new Ellipse(); tr.Stroke = Theme.Track; tr.StrokeThickness = thick;
        g.Children.Add(tr);
        double r = (d - thick) / 2, c = d / 2;
        double frac = Math.Max(0.006, Math.Min(0.9999, it.Percent / 100.0));
        double a = frac * 2 * Math.PI;
        PathFigure f = new PathFigure(); f.StartPoint = new Point(c, c - r);
        f.Segments.Add(new ArcSegment(new Point(c + r * Math.Sin(a), c - r * Math.Cos(a)), new Size(r, r), 0, a > Math.PI, SweepDirection.Clockwise, true));
        PathGeometry pg = new PathGeometry(); pg.Figures.Add(f);
        Path p = new Path(); p.Data = pg; p.Stroke = BatBrush(it); p.StrokeThickness = thick;
        p.StrokeStartLineCap = PenLineCap.Round; p.StrokeEndLineCap = PenLineCap.Round;
        p.HorizontalAlignment = HorizontalAlignment.Left; p.VerticalAlignment = VerticalAlignment.Top;
        g.Children.Add(p);
        TextBlock t = T(it.Percent + "%", fs, Sb, Theme.Fg);
        t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(t);
        return g;
    }

    static UIElement Tile(BatItem it, double ring, double thick, double pfs, double nfs, double maxW)
    {
        StackPanel s = new StackPanel(); s.HorizontalAlignment = HorizontalAlignment.Center; s.VerticalAlignment = VerticalAlignment.Center;
        s.Children.Add(Ring(it, ring, thick, pfs));
        TextBlock n = Ctr(T(BatName(it), nfs, Nm, Theme.Sub));
        n.TextTrimming = TextTrimming.CharacterEllipsis; n.MaxWidth = maxW; n.Margin = new Thickness(0, 6, 0, 0);
        s.Children.Add(n);
        return s;
    }

    static UIElement BarRow(BatItem it, double nfs, double pfs, double barH)
    {
        StackPanel s = new StackPanel(); s.VerticalAlignment = VerticalAlignment.Center; s.Margin = new Thickness(0, 2, 0, 2);
        Grid top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        TextBlock n = T(BatName(it), nfs, Sb, Theme.Fg); n.TextTrimming = TextTrimming.CharacterEllipsis; n.VerticalAlignment = VerticalAlignment.Center;
        top.Children.Add(Cell(n, 0, 0));
        TextBlock p = T(it.Percent + "%", pfs, pfs > 20 ? Lt : Sb, Theme.Fg); p.VerticalAlignment = VerticalAlignment.Center;
        top.Children.Add(Cell(p, 0, 1));
        s.Children.Add(top);
        Grid bar = new Grid(); bar.Height = barH; bar.Margin = new Thickness(0, 4, 0, 0);
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.5, it.Percent), GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.0001, 100 - it.Percent), GridUnitType.Star) });
        Border track = new Border(); track.CornerRadius = new CornerRadius(barH / 2); track.Background = Theme.Track;
        Grid.SetColumnSpan(track, 2);
        bar.Children.Add(track);
        Border fill = new Border(); fill.CornerRadius = new CornerRadius(barH / 2); fill.Background = BatBrush(it);
        bar.Children.Add(Cell(fill, 0, 0));
        s.Children.Add(bar);
        return s;
    }

    static WContent BatteryW(int style, Mode m)
    {
        return Dyn(m, delegate
        {
            string s = "";
            foreach (BatItem b in Bat.Items) s += b.Name + ":" + b.Percent + ":" + b.Charging + ";";
            return s;
        }, delegate
        {
            List<BatItem> all = Bat.Items;
            int max = m == Mode.S ? 1 : (m == Mode.M ? (style == 0 ? 4 : 3) : 6);
            List<BatItem> items = all.Take(max).ToList();
            if (items.Count == 0)
            {
                StackPanel p = new StackPanel(); p.VerticalAlignment = VerticalAlignment.Center; p.HorizontalAlignment = HorizontalAlignment.Center;
                TextBlock ic = Ctr(T("", 30, Nm, Theme.Sub)); ic.FontFamily = U.Icons;
                p.Children.Add(ic);
                TextBlock t = Ctr(T("No battery", 14, Sb, Theme.Sub)); t.Margin = new Thickness(0, 6, 0, 0);
                p.Children.Add(t);
                return p;
            }
            Size sz = Inner(m);
            if (style == 0)
            {
                if (m == Mode.S) return Tile(items[0], 84, 9, 20, 12, 126);
                if (m == Mode.M)
                {
                    UniformGrid u = new UniformGrid(); u.Rows = 1; u.Columns = items.Count;
                    double ring = items.Count <= 2 ? 84 : 62;
                    foreach (BatItem b in items) u.Children.Add(Tile(b, ring, items.Count <= 2 ? 9 : 7, items.Count <= 2 ? 20 : 15, 11, 300.0 / items.Count - 4));
                    return u;
                }
                UniformGrid lg = new UniformGrid(); lg.Columns = 3;
                foreach (BatItem b in items) lg.Children.Add(Tile(b, items.Count <= 3 ? 84 : 72, 8, items.Count <= 3 ? 20 : 17, 12, 96));
                return lg;
            }
            if (m == Mode.S) return BarRow(items[0], 13, 36, 9);
            Grid g = new Grid();
            for (int i = 0; i < items.Count; i++) g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(sz.Height / (m == Mode.M ? 3 : 6)) });
            g.VerticalAlignment = VerticalAlignment.Center;
            for (int i = 0; i < items.Count; i++)
                g.Children.Add(Cell(BarRow(items[i], m == Mode.M ? 13 : 14, m == Mode.M ? 14 : 15, m == Mode.M ? 6 : 8), i, 0));
            return g;
        });
    }

    // ================= NOTE =================
    static WContent NoteW(int style, Mode m, WidgetCfg cfg)
    {
        WContent c = new WContent(); c.Fluid = true;
        bool yellow = style == 0;
        Brush fg = yellow ? Theme.B(255, 62, 48, 0) : Theme.Fg;
        Brush hint = yellow ? Theme.B(120, 62, 48, 0) : Theme.Faint;
        if (yellow) c.Tint = Color.FromArgb((byte)(120 + Math.Max(0, Math.Min(1, Cfg.Data.Strength)) * 90), 255, 214, 10);
        Grid g = new Grid();
        TextBox tb = new TextBox();
        tb.Background = Brushes.Transparent; tb.BorderThickness = new Thickness(0); tb.Foreground = fg; tb.CaretBrush = fg;
        tb.AcceptsReturn = true; tb.TextWrapping = TextWrapping.Wrap; tb.Padding = new Thickness(0);
        tb.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        tb.FontSize = m == Mode.S ? 14 : 16; tb.FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        tb.SelectionBrush = Theme.B(120, 10, 132, 255);
        TextBlock ph = T("Write a note…", tb.FontSize, Nm, hint); ph.IsHitTestVisible = false; ph.VerticalAlignment = VerticalAlignment.Top;
        if (cfg == null)
        {
            tb.Text = "Buy milk\nCall mom\nWater the plants";
            tb.IsReadOnly = true; tb.IsHitTestVisible = false; ph.Visibility = Visibility.Collapsed;
        }
        else
        {
            tb.Text = cfg.Text ?? "";
            ph.Visibility = tb.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            tb.TextChanged += delegate
            {
                cfg.Text = tb.Text; Cfg.Touch();
                ph.Visibility = tb.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            };
        }
        g.Children.Add(ph); g.Children.Add(tb);
        c.Root = g; c.NoDrag = tb; c.Update = delegate { };
        return c;
    }
}

// ---------- widget window ----------
class WidgetWindow : GlassWindow
{
    public WidgetCfg Wc;
    WContent cur;
    Mode mode;
    bool built, dragging, resizing;
    Border faceHost;
    FrameworkElement grip;
    Point startCur; double sx, sy, sw, sh;
    MenuItem[] styleItems = new MenuItem[2];
    MenuItem[] sizeItems = new MenuItem[3];

    public WidgetWindow(WidgetCfg c)
    {
        Wc = c; NoActivate = c.Type != "Note";
        Title = "MacLookLike " + c.Type;
        Width = c.W; Height = c.H; Left = c.X; Top = c.Y;
        Grid root = new Grid();
        faceHost = new Border(); root.Children.Add(faceHost);
        grip = MakeGrip(); root.Children.Add(grip);
        Content = root;
        Rebuild();
        Clamp();
        SizeChanged += OnSized;
        LocationChanged += delegate { if (built) { Wc.X = Left; Wc.Y = Top; Cfg.Touch(); } };
        MouseEnter += delegate { grip.Opacity = 1; };
        MouseLeave += delegate { if (!resizing) grip.Opacity = 0; };
        BuildMenu();
    }

    FrameworkElement MakeGrip()
    {
        Grid g = new Grid(); g.Width = 26; g.Height = 26;
        g.HorizontalAlignment = HorizontalAlignment.Right; g.VerticalAlignment = VerticalAlignment.Bottom;
        g.Background = Brushes.Transparent; g.Cursor = Cursors.SizeNWSE; g.Opacity = 0;
        Canvas c = new Canvas(); c.Width = 26; c.Height = 26; c.IsHitTestVisible = false;
        double[][] ls = { new double[] { 21, 9, 9, 21 }, new double[] { 21, 15, 15, 21 } };
        foreach (double[] a in ls)
        {
            Line l = new Line(); l.X1 = a[0]; l.Y1 = a[1]; l.X2 = a[2]; l.Y2 = a[3];
            l.Stroke = Theme.Sub; l.StrokeThickness = 1.6; l.StrokeStartLineCap = PenLineCap.Round; l.StrokeEndLineCap = PenLineCap.Round;
            c.Children.Add(l);
        }
        g.Children.Add(c);
        return g;
    }

    public void Rebuild()
    {
        mode = WF.ModeFor(Width, Height);
        cur = WF.Build(Wc, mode);
        faceHost.Child = WF.Face(cur);
        built = true;
        // refresh grip color for the current theme
        Grid g = grip as Grid;
        if (g != null && g.Children.Count > 0)
        {
            Canvas c = g.Children[0] as Canvas;
            if (c != null) foreach (UIElement u in c.Children) { Line l = u as Line; if (l != null) l.Stroke = Theme.Sub; }
        }
    }

    public void Tick() { if (cur != null && cur.Update != null) cur.Update(); }

    void OnSized(object s, SizeChangedEventArgs e)
    {
        if (!built) return;
        Mode m = WF.ModeFor(Width, Height);
        if (m != mode)
        {
            if (cur != null && cur.Fluid) mode = m; else Rebuild();
        }
        Wc.W = Width; Wc.H = Height; Cfg.Touch();
    }

    static bool Inside(DependencyObject d, DependencyObject anc)
    {
        while (d != null)
        {
            if (d == anc) return true;
            if (d is Visual || d is System.Windows.Media.Media3D.Visual3D) d = VisualTreeHelper.GetParent(d);
            else d = LogicalTreeHelper.GetParent(d);
        }
        return false;
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        DependencyObject o = e.OriginalSource as DependencyObject;
        if (cur != null && cur.NoDrag != null && Inside(o, cur.NoDrag)) return;
        Native.POINT p; Native.GetCursorPos(out p);
        double s = Scale();
        startCur = new Point(p.X / s, p.Y / s);
        sx = Left; sy = Top; sw = Width; sh = Height;
        resizing = Inside(o, grip); dragging = !resizing;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging && !resizing) return;
        if (e.LeftButton != MouseButtonState.Pressed) { End(); return; }
        Native.POINT p; Native.GetCursorPos(out p);
        double s = Scale();
        double dx = p.X / s - startCur.X, dy = p.Y / s - startCur.Y;
        if (dragging) { Left = sx + dx; Top = sy + dy; }
        else
        {
            Width = Math.Max(110, Math.Min(760, sw + dx));
            Height = Math.Max(110, Math.Min(760, sh + dy));
        }
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        End();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        dragging = false; resizing = false;
    }

    void End()
    {
        if (!dragging && !resizing) return;
        bool wasDrag = dragging;
        dragging = false; resizing = false;
        ReleaseMouseCapture();
        if (wasDrag) Clamp();
        if (!IsMouseOver) grip.Opacity = 0;
        Wc.X = Left; Wc.Y = Top; Wc.W = Width; Wc.H = Height; Cfg.Touch();
    }

    void Clamp()
    {
        double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
        double vr = vl + SystemParameters.VirtualScreenWidth, vb = vt + SystemParameters.VirtualScreenHeight;
        Left = Math.Max(vl - Width + 60, Math.Min(vr - 60, Left));
        Top = Math.Max(vt, Math.Min(vb - 40, Top));
    }

    public void SetMode(Mode m)
    {
        Size d = WF.Dim(m);
        Width = d.Width; Height = d.Height;
        Rebuild(); Clamp();
        Wc.W = Width; Wc.H = Height; Cfg.Touch();
    }

    public void SetStyle(int s)
    {
        Wc.Style = s; Rebuild(); Cfg.Touch();
    }

    void BuildMenu()
    {
        ContextMenu m = new ContextMenu();
        MenuItem set = new MenuItem(); set.Header = "MacLookLike Settings…";
        set.FontWeight = FontWeights.SemiBold;
        set.Click += delegate { SettingsWindow.Open(); };
        m.Items.Add(set);
        m.Items.Add(new Separator());
        MenuItem st = new MenuItem(); st.Header = "Style";
        for (int i = 0; i < 2; i++)
        {
            int idx = i;
            MenuItem it = new MenuItem(); it.Header = WF.StyleName(Wc.Type, i); it.IsCheckable = false;
            it.Click += delegate { SetStyle(idx); };
            styleItems[i] = it; st.Items.Add(it);
        }
        m.Items.Add(st);
        MenuItem sz = new MenuItem(); sz.Header = "Size";
        string[] names = { "Small", "Medium", "Large" };
        for (int i = 0; i < 3; i++)
        {
            Mode mm = (Mode)i;
            MenuItem it = new MenuItem(); it.Header = names[i];
            it.Click += delegate { SetMode(mm); };
            sizeItems[i] = it; sz.Items.Add(it);
        }
        m.Items.Add(sz);
        m.Items.Add(new Separator());
        MenuItem rm = new MenuItem(); rm.Header = "Remove Widget";
        rm.Click += delegate { WidgetManager.Remove(Wc); };
        m.Items.Add(rm);
        m.Opened += delegate
        {
            for (int i = 0; i < 2; i++) styleItems[i].IsChecked = Wc.Style == i;
            for (int i = 0; i < 3; i++) sizeItems[i].IsChecked = (int)mode == i;
        };
        ContextMenu = m;
    }
}

// ---------- slim top bar ----------
class MenuBarWindow : GlassWindow
{
    TextBlock bat, date, time;

    public MenuBarWindow()
    {
        Radius = 0; Title = "MacLookLike Menu Bar";
        Left = 0; Top = 0; Width = SystemParameters.PrimaryScreenWidth; Height = 28;
        Rebuild();
        ContextMenu m = new ContextMenu();
        MenuItem set = new MenuItem(); set.Header = "MacLookLike Settings…"; set.FontWeight = FontWeights.SemiBold;
        set.Click += delegate { SettingsWindow.Open(); };
        m.Items.Add(set);
        MenuItem hide = new MenuItem(); hide.Header = "Hide Menu Bar";
        hide.Click += delegate { Cfg.Data.MenuBar = false; Cfg.Touch(); WidgetManager.ApplyMenuBar(); };
        m.Items.Add(hide);
        ContextMenu = m;
    }

    public void Rebuild()
    {
        Grid g = new Grid();
        Border bg = new Border(); bg.Background = Theme.TintBrush; g.Children.Add(bg);
        Border line = new Border(); line.Height = 1; line.Background = Theme.Divider; line.VerticalAlignment = VerticalAlignment.Bottom; g.Children.Add(line);
        TextBlock name = U.T("MacLookLike", 13, FontWeights.Bold, Theme.Fg);
        name.VerticalAlignment = VerticalAlignment.Center; name.Margin = new Thickness(14, 0, 0, 0);
        g.Children.Add(name);
        StackPanel right = new StackPanel(); right.Orientation = Orientation.Horizontal;
        right.HorizontalAlignment = HorizontalAlignment.Right; right.VerticalAlignment = VerticalAlignment.Center; right.Margin = new Thickness(0, 0, 14, 0);
        bat = U.T("", 13, FontWeights.Normal, Theme.Fg); bat.Margin = new Thickness(0, 0, 16, 0);
        date = U.T("", 13, FontWeights.Normal, Theme.Fg); date.Margin = new Thickness(0, 0, 10, 0);
        time = U.T("", 13, FontWeights.SemiBold, Theme.Fg);
        right.Children.Add(bat); right.Children.Add(date); right.Children.Add(time);
        g.Children.Add(right);
        Content = g;
        Tick();
    }

    public void Tick()
    {
        AppCfg c = Cfg.Data;
        CultureInfo e = new CultureInfo("en-US");
        DateTime n = DateTime.Now;
        BatItem pc = null;
        foreach (BatItem b in Bat.Items) if (!b.Bt) { pc = b; break; }
        bat.Visibility = c.MenuBattery && pc != null ? Visibility.Visible : Visibility.Collapsed;
        if (pc != null) bat.Text = (pc.Charging ? "⚡ " : "") + pc.Percent + "%";
        date.Visibility = c.MenuDate ? Visibility.Visible : Visibility.Collapsed;
        time.Visibility = c.MenuDate ? Visibility.Visible : Visibility.Collapsed;
        date.Text = n.ToString("ddd d MMM", e);
        time.Text = n.ToString(c.Hour24 ? "HH:mm" : "h:mm tt", e);
    }
}

static class WidgetManager
{
    static readonly List<WidgetWindow> wins = new List<WidgetWindow>();
    static MenuBarWindow bar;
    static DispatcherTimer timer;

    public static void LoadAll()
    {
        foreach (WidgetCfg c in Cfg.Data.Widgets.ToList()) Show(c);
        timer = new DispatcherTimer(); timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += delegate
        {
            foreach (WidgetWindow w in wins) w.Tick();
            if (bar != null) bar.Tick();
        };
        timer.Start();
    }

    static WidgetWindow Show(WidgetCfg c)
    {
        WidgetWindow w = new WidgetWindow(c);
        wins.Add(w); w.Show();
        return w;
    }

    public static WidgetCfg AddNew(string type, int style, Mode m)
    {
        Size d = WF.Dim(m);
        WidgetCfg c = Cfg.NewWidget(type, style, d.Width, d.Height);
        Show(c);
        return c;
    }

    public static WidgetWindow Find(WidgetCfg c)
    {
        foreach (WidgetWindow w in wins) if (w.Wc == c) return w;
        return null;
    }

    public static void Remove(WidgetCfg c)
    {
        WidgetWindow w = Find(c);
        if (w != null) { wins.Remove(w); w.Close(); }
        Cfg.Data.Widgets.Remove(c);
        Cfg.Touch();
        SettingsWindow.RefreshIfOpen();
    }

    public static void RebuildAll()
    {
        foreach (WidgetWindow w in wins) w.Rebuild();
        if (bar != null) bar.Rebuild();
    }

    public static void ApplyMenuBar()
    {
        if (Cfg.Data.MenuBar)
        {
            if (bar == null) { bar = new MenuBarWindow(); bar.Show(); }
            else bar.Rebuild();
        }
        else if (bar != null) { bar.Close(); bar = null; }
    }

    public static void CloseAll()
    {
        foreach (WidgetWindow w in wins.ToList()) w.Close();
        wins.Clear();
        if (bar != null) { bar.Close(); bar = null; }
    }
}
