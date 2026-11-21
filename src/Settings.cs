using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

class SettingsWindow : Window
{
    static SettingsWindow inst;
    string page = "General";
    string search = "";
    bool big;
    TextBlock status;

    public static void Open()
    {
        if (inst == null) { inst = new SettingsWindow(); inst.Show(); }
        if (inst.WindowState == WindowState.Minimized) inst.WindowState = WindowState.Normal;
        inst.Activate();
    }

    public static void RefreshIfOpen() { if (inst != null) inst.Build(); }

    public SettingsWindow()
    {
        Title = "MacLookLike Settings";
        Width = 820; Height = 580;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Transparent; ShowInTaskbar = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Closed += delegate { inst = null; };
        Build();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        HwndSource src = (HwndSource)PresentationSource.FromVisual(this);
        src.CompositionTarget.BackgroundColor = Colors.Transparent;
        Native.ExtendFrame(src.Handle);
        Native.EnableBlur(src.Handle);
        Native.RoundCorners(src.Handle);
    }

    // ---------- UI helpers ----------
    static TextBlock T(string s, double size, FontWeight w, Brush b) { return U.T(s, size, w, b); }

    static FrameworkElement Toggle(bool on, Action<bool> set)
    {
        Border b = new Border(); b.Width = 44; b.Height = 26; b.CornerRadius = new CornerRadius(13); b.Cursor = Cursors.Hand;
        Ellipse k = new Ellipse(); k.Width = 22; k.Height = 22; k.Fill = Brushes.White; k.VerticalAlignment = VerticalAlignment.Center;
        b.Child = k;
        bool state = on;
        Action paint = delegate
        {
            b.Background = state ? Theme.B(Theme.Green) : Theme.Track;
            k.HorizontalAlignment = state ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            k.Margin = new Thickness(2);
        };
        paint();
        b.MouseLeftButtonUp += delegate { state = !state; paint(); set(state); };
        b.VerticalAlignment = VerticalAlignment.Center;
        return b;
    }

    static FrameworkElement Segment(string[] items, int sel, Action<int> pick)
    {
        Border outer = new Border(); outer.CornerRadius = new CornerRadius(8); outer.Background = Theme.Track; outer.Padding = new Thickness(2);
        outer.VerticalAlignment = VerticalAlignment.Center;
        StackPanel sp = new StackPanel(); sp.Orientation = Orientation.Horizontal;
        outer.Child = sp;
        List<Border> bs = new List<Border>();
        List<TextBlock> ts = new List<TextBlock>();
        int cur = sel;
        Action paint = delegate
        {
            for (int i = 0; i < bs.Count; i++)
            {
                bs[i].Background = i == cur ? Theme.B(Theme.Blue) : Brushes.Transparent;
                ts[i].Foreground = i == cur ? Brushes.White : Theme.Fg;
            }
        };
        for (int i = 0; i < items.Length; i++)
        {
            int idx = i;
            Border b = new Border(); b.CornerRadius = new CornerRadius(6); b.Padding = new Thickness(12, 4, 12, 4); b.Cursor = Cursors.Hand;
            b.Background = Brushes.Transparent;
            TextBlock t = T(items[i], 13, FontWeights.Medium, Theme.Fg);
            b.Child = t;
            b.MouseLeftButtonUp += delegate { cur = idx; paint(); pick(idx); };
            bs.Add(b); ts.Add(t); sp.Children.Add(b);
        }
        paint();
        return outer;
    }

    static FrameworkElement Btn(string text, Action click, bool danger)
    {
        Border b = new Border(); b.CornerRadius = new CornerRadius(7); b.Padding = new Thickness(14, 5, 14, 5); b.Cursor = Cursors.Hand;
        b.Background = danger ? Theme.B(Theme.Red) : Theme.Track; b.VerticalAlignment = VerticalAlignment.Center;
        b.Child = T(text, 13, FontWeights.Medium, danger ? Brushes.White : Theme.Fg);
        b.MouseLeftButtonUp += delegate { click(); };
        return b;
    }

    static FrameworkElement Row(string title, string sub, FrameworkElement ctl)
    {
        Grid g = new Grid(); g.Margin = new Thickness(14, 10, 14, 10);
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        StackPanel l = new StackPanel(); l.VerticalAlignment = VerticalAlignment.Center;
        l.Children.Add(T(title, 14, FontWeights.Normal, Theme.Fg));
        if (!string.IsNullOrEmpty(sub))
        {
            TextBlock s = T(sub, 12, FontWeights.Normal, Theme.Sub); s.TextWrapping = TextWrapping.Wrap; s.MaxWidth = 300; s.Margin = new Thickness(0, 2, 0, 0);
            l.Children.Add(s);
        }
        g.Children.Add(l);
        if (ctl != null) { Grid.SetColumn(ctl, 1); ctl.Margin = new Thickness(12, 0, 0, 0); g.Children.Add(ctl); }
        return g;
    }

    static FrameworkElement Card(params FrameworkElement[] rows)
    {
        Border b = new Border(); b.CornerRadius = new CornerRadius(10); b.Background = Theme.CardBg; b.Margin = new Thickness(0, 0, 0, 18);
        StackPanel sp = new StackPanel();
        for (int i = 0; i < rows.Length; i++)
        {
            if (i > 0) { Border d = new Border(); d.Height = 1; d.Background = Theme.Divider; d.Margin = new Thickness(14, 0, 0, 0); sp.Children.Add(d); }
            sp.Children.Add(rows[i]);
        }
        b.Child = sp;
        return b;
    }

    static TextBlock Head(string s)
    {
        TextBlock t = T(s.ToUpper(), 11, FontWeights.SemiBold, Theme.Sub); t.Margin = new Thickness(6, 0, 0, 6);
        return t;
    }

    static string ExePath() { return System.Reflection.Assembly.GetExecutingAssembly().Location; }

    static void SetLaunch(bool on)
    {
        try
        {
            RegistryKey k = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (k == null) return;
            if (on) k.SetValue("MacLookLike", "\"" + ExePath() + "\" --background");
            else k.DeleteValue("MacLookLike", false);
            k.Close();
        }
        catch (Exception ex) { Cfg.LogError("launch: " + ex.Message); }
    }

    // ---------- layout ----------
    void Build()
    {
        Theme.Apply();
        Grid root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        FrameworkElement side = BuildSide();
        root.Children.Add(side);
        Grid pane = new Grid(); pane.Background = Theme.Pane;
        Grid.SetColumn(pane, 1);
        pane.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
        pane.RowDefinitions.Add(new RowDefinition());
        Border titleBar = new Border(); titleBar.Background = Brushes.Transparent;
        titleBar.Child = T(page, 20, FontWeights.Bold, Theme.Fg);
        ((TextBlock)titleBar.Child).VerticalAlignment = VerticalAlignment.Center;
        ((TextBlock)titleBar.Child).Margin = new Thickness(26, 0, 0, 0);
        titleBar.MouseLeftButtonDown += delegate { try { DragMove(); } catch { } };
        pane.Children.Add(titleBar);
        ScrollViewer sv = new ScrollViewer(); sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Grid.SetRow(sv, 1);
        StackPanel body = new StackPanel(); body.Margin = new Thickness(24, 4, 24, 24);
        status = null;
        if (page == "General") PageGeneral(body);
        else if (page == "Appearance") PageAppearance(body);
        else if (page == "Menu Bar") PageMenuBar(body);
        else PageWidgets(body);
        sv.Content = body;
        pane.Children.Add(sv);
        root.Children.Add(pane);
        Content = root;
    }

    FrameworkElement Light(Color c, string glyph, Action click)
    {
        Grid g = new Grid(); g.Width = 14; g.Height = 14; g.Margin = new Thickness(0, 0, 8, 0); g.Cursor = Cursors.Hand; g.Background = Brushes.Transparent;
        Ellipse e = new Ellipse(); e.Fill = IsActive ? Theme.B(c) : Theme.B(90, 140, 140, 140);
        e.Stroke = Theme.B(60, 0, 0, 0); e.StrokeThickness = 0.5;
        g.Children.Add(e);
        TextBlock t = T(glyph, 9, FontWeights.Bold, Theme.B(190, 40, 20, 10)); t.FontFamily = U.Icons;
        t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; t.Opacity = 0;
        g.Children.Add(t);
        g.MouseEnter += delegate { t.Opacity = 1; };
        g.MouseLeave += delegate { t.Opacity = 0; };
        g.MouseLeftButtonUp += delegate { click(); };
        return g;
    }

    FrameworkElement BuildSide()
    {
        Grid side = new Grid(); side.Background = Theme.SideBg;
        side.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        side.RowDefinitions.Add(new RowDefinition());
        // traffic lights (also the drag handle)
        Border drag = new Border(); drag.Background = Brushes.Transparent;
        drag.MouseLeftButtonDown += delegate { try { DragMove(); } catch { } };
        StackPanel lights = new StackPanel(); lights.Orientation = Orientation.Horizontal; lights.Margin = new Thickness(18, 0, 0, 0); lights.VerticalAlignment = VerticalAlignment.Center;
        lights.Children.Add(Light(Color.FromRgb(255, 95, 87), "", delegate { Close(); }));
        lights.Children.Add(Light(Color.FromRgb(254, 188, 46), "", delegate { WindowState = WindowState.Minimized; }));
        lights.Children.Add(Light(Color.FromRgb(40, 200, 64), "", delegate
        {
            big = !big;
            double cx = Left + Width / 2, cy = Top + Height / 2;
            Width = big ? 1000 : 820; Height = big ? 700 : 580;
            Left = cx - Width / 2; Top = cy - Height / 2;
        }));
        drag.Child = lights;
        side.Children.Add(drag);

        // search
        Border sb = new Border(); sb.CornerRadius = new CornerRadius(8); sb.Background = Theme.Track; sb.Margin = new Thickness(14, 0, 14, 12); sb.Padding = new Thickness(8, 5, 8, 5);
        Grid sg = new Grid();
        sg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sg.ColumnDefinitions.Add(new ColumnDefinition());
        TextBlock mag = T("", 12, FontWeights.Normal, Theme.Sub); mag.FontFamily = U.Icons; mag.VerticalAlignment = VerticalAlignment.Center; mag.Margin = new Thickness(0, 0, 7, 0);
        sg.Children.Add(mag);
        TextBox tb = new TextBox(); tb.Background = Brushes.Transparent; tb.BorderThickness = new Thickness(0); tb.Foreground = Theme.Fg; tb.CaretBrush = Theme.Fg;
        tb.FontSize = 13; tb.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(tb, 1); sg.Children.Add(tb);
        sb.Child = sg;
        Grid.SetRow(sb, 1); side.Children.Add(sb);

        // profile
        Grid prof = new Grid(); prof.Margin = new Thickness(18, 0, 14, 14);
        prof.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        prof.ColumnDefinitions.Add(new ColumnDefinition());
        string name = UserInfo.DisplayName();
        Grid av = new Grid(); av.Width = 48; av.Height = 48;
        Ellipse ae = new Ellipse();
        ImageSource pic = UserInfo.Picture();
        if (pic != null) { ImageBrush ib = new ImageBrush(pic); ib.Stretch = Stretch.UniformToFill; ae.Fill = ib; }
        else ae.Fill = Theme.B(Theme.Blue);
        av.Children.Add(ae);
        if (pic == null)
        {
            TextBlock ini = T(string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpper(), 22, FontWeights.SemiBold, Brushes.White);
            ini.HorizontalAlignment = HorizontalAlignment.Center; ini.VerticalAlignment = VerticalAlignment.Center;
            av.Children.Add(ini);
        }
        prof.Children.Add(av);
        StackPanel pn = new StackPanel(); pn.VerticalAlignment = VerticalAlignment.Center; pn.Margin = new Thickness(10, 0, 0, 0);
        TextBlock nm = T(name, 15, FontWeights.SemiBold, Theme.Fg); nm.TextTrimming = TextTrimming.CharacterEllipsis;
        pn.Children.Add(nm);
        pn.Children.Add(T("Windows Account", 12, FontWeights.Normal, Theme.Sub));
        Grid.SetColumn(pn, 1); prof.Children.Add(pn);
        Grid.SetRow(prof, 2); side.Children.Add(prof);

        // items
        StackPanel items = new StackPanel(); items.Margin = new Thickness(10, 0, 10, 0);
        Grid.SetRow(items, 3); side.Children.Add(items);
        string[] names = { "General", "Appearance", "Menu Bar", "Desktop Widgets" };
        string[] glyphs = { "", "", "", "" };
        Color[] cols = { Color.FromRgb(142, 142, 147), Color.FromRgb(10, 132, 255), Color.FromRgb(88, 86, 214), Color.FromRgb(255, 159, 10) };
        string[] keys = { "general settings launch login 24 hour fahrenheit weather location quit about",
                          "appearance tint dark light clear strength glass widgets customize style size",
                          "menu bar widget bar battery date time top",
                          "desktop widgets add clock calendar weather battery note" };
        List<Border> rows = new List<Border>();
        for (int i = 0; i < 4; i++)
        {
            string pg = names[i];
            Border r = new Border(); r.CornerRadius = new CornerRadius(8); r.Padding = new Thickness(8, 6, 8, 6); r.Margin = new Thickness(0, 1, 0, 1); r.Cursor = Cursors.Hand;
            r.Background = pg == page ? Theme.B(70, 10, 132, 255) : Brushes.Transparent;
            StackPanel h = new StackPanel(); h.Orientation = Orientation.Horizontal;
            Border ic = new Border(); ic.Width = 26; ic.Height = 26; ic.CornerRadius = new CornerRadius(6); ic.Background = Theme.B(cols[i]);
            TextBlock gl = T(glyphs[i], 14, FontWeights.Normal, Brushes.White); gl.FontFamily = U.Icons;
            gl.HorizontalAlignment = HorizontalAlignment.Center; gl.VerticalAlignment = VerticalAlignment.Center;
            ic.Child = gl;
            h.Children.Add(ic);
            TextBlock lb = T(pg, 14, FontWeights.Normal, Theme.Fg); lb.VerticalAlignment = VerticalAlignment.Center; lb.Margin = new Thickness(10, 0, 0, 0);
            h.Children.Add(lb);
            r.Child = h; r.Tag = keys[i] + " " + pg.ToLower();
            r.MouseLeftButtonUp += delegate { page = pg; Build(); };
            rows.Add(r); items.Children.Add(r);
        }
        tb.Text = search;
        Action filter = delegate
        {
            string q = tb.Text.Trim().ToLower();
            search = tb.Text;
            foreach (Border r in rows) r.Visibility = (q.Length == 0 || ((string)r.Tag).Contains(q)) ? Visibility.Visible : Visibility.Collapsed;
        };
        tb.TextChanged += delegate { filter(); };
        filter();
        return side;
    }

    // ---------- pages ----------
    void PageGeneral(StackPanel p)
    {
        AppCfg c = Cfg.Data;
        p.Children.Add(Head("Startup"));
        p.Children.Add(Card(Row("Launch at login", "Show widgets when you sign in to Windows", Toggle(c.Launch, delegate(bool v) { c.Launch = v; Cfg.Touch(); SetLaunch(v); }))));
        p.Children.Add(Head("Format"));
        p.Children.Add(Card(
            Row("24-hour time", null, Toggle(c.Hour24, delegate(bool v) { c.Hour24 = v; Cfg.Touch(); WidgetManager.RebuildAll(); })),
            Row("Temperature in °F", null, Toggle(c.Fahrenheit, delegate(bool v) { c.Fahrenheit = v; Cfg.Touch(); WidgetManager.RebuildAll(); }))));

        p.Children.Add(Head("Weather"));
        TextBox city = new TextBox(); city.Width = 150; city.FontSize = 13; city.Padding = new Thickness(6, 4, 6, 4);
        city.Background = Theme.Track; city.Foreground = Theme.Fg; city.CaretBrush = Theme.Fg; city.BorderThickness = new Thickness(0);
        city.Text = c.AutoLoc ? "" : (c.City ?? "");
        status = T(c.AutoLoc ? "Using your approximate location" : "Location: " + c.City, 12, FontWeights.Normal, Theme.Sub);
        status.Margin = new Thickness(14, 0, 14, 10); status.TextWrapping = TextWrapping.Wrap;
        TextBlock st = status;
        Action doSearch = delegate
        {
            string q = city.Text.Trim();
            if (q.Length == 0) return;
            st.Text = "Searching…";
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string msg; bool ok = Weather.Search(q, out msg);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    st.Text = msg;
                    if (ok) { Weather.RefreshAsync(); WidgetManager.RebuildAll(); RefreshIfOpen(); }
                }));
            });
        };
        city.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter) doSearch(); };
        StackPanel cr = new StackPanel(); cr.Orientation = Orientation.Horizontal;
        cr.Children.Add(city);
        FrameworkElement go = Btn("Search", doSearch, false); go.Margin = new Thickness(8, 0, 0, 0);
        cr.Children.Add(go);
        Border cardHold = new Border();
        StackPanel cs = new StackPanel();
        cs.Children.Add(Row("Detect location automatically", "Sends your IP address to ipwho.is to find your city", Toggle(c.AutoLoc, delegate(bool v)
        {
            c.AutoLoc = v; Cfg.Touch(); Weather.RefreshAsync();
            st.Text = v ? "Using your approximate location" : "Type a city below";
        })));
        Border d1 = new Border(); d1.Height = 1; d1.Background = Theme.Divider; d1.Margin = new Thickness(14, 0, 0, 0);
        cs.Children.Add(d1);
        cs.Children.Add(Row("City", null, cr));
        Border d2 = new Border(); d2.Height = 1; d2.Background = Theme.Divider; d2.Margin = new Thickness(14, 0, 0, 0);
        cs.Children.Add(d2);
        cs.Children.Add(st);
        Border cb = new Border(); cb.CornerRadius = new CornerRadius(10); cb.Background = Theme.CardBg; cb.Margin = new Thickness(0, 0, 0, 18);
        cb.Child = cs;
        p.Children.Add(cb);

        p.Children.Add(Head("About"));
        p.Children.Add(Card(
            Row("MacLookLike", "Glass desktop widgets for Windows", null),
            Row("Quit MacLookLike", "Closes all widgets", Btn("Quit", delegate { Program.Quit(); }, true))));
    }

    void PageAppearance(StackPanel p)
    {
        AppCfg c = Cfg.Data;
        p.Children.Add(Head("Glass"));
        Slider sl = new Slider(); sl.Minimum = 0; sl.Maximum = 1; sl.Value = c.Strength; sl.Width = 160; sl.VerticalAlignment = VerticalAlignment.Center;
        sl.ValueChanged += delegate
        {
            c.Strength = sl.Value; Cfg.Touch(); Theme.Apply(); WidgetManager.RebuildAll();
        };
        p.Children.Add(Card(
            Row("Tint", "Dark, light or clear glass", Segment(new string[] { "Dark", "Light", "Clear" }, c.Tint, delegate(int i)
            {
                c.Tint = i; Cfg.Touch(); Theme.Apply(); WidgetManager.RebuildAll(); Build();
            })),
            Row("Tint strength", "How opaque the glass is", sl)));
        p.Children.Add(Head("Your widgets"));
        if (c.Widgets.Count == 0)
        {
            p.Children.Add(Card(Row("No widgets", "Add some from Desktop Widgets", null)));
            return;
        }
        List<FrameworkElement> rows = new List<FrameworkElement>();
        foreach (WidgetCfg w in c.Widgets.ToArray())
        {
            WidgetCfg wc = w;
            StackPanel ctl = new StackPanel(); ctl.Orientation = Orientation.Horizontal;
            FrameworkElement s1 = Segment(new string[] { WF.StyleName(wc.Type, 0), WF.StyleName(wc.Type, 1) }, Math.Max(0, Math.Min(1, wc.Style)), delegate(int i)
            {
                WidgetWindow ww = WidgetManager.Find(wc); if (ww != null) ww.SetStyle(i);
            });
            FrameworkElement s2 = Segment(new string[] { "S", "M", "L" }, (int)WF.ModeFor(wc.W, wc.H), delegate(int i)
            {
                WidgetWindow ww = WidgetManager.Find(wc); if (ww != null) ww.SetMode((Mode)i);
            });
            s2.Margin = new Thickness(8, 0, 0, 0);
            FrameworkElement rm = Btn("Remove", delegate { WidgetManager.Remove(wc); }, false); rm.Margin = new Thickness(8, 0, 0, 0);
            ctl.Children.Add(s1); ctl.Children.Add(s2); ctl.Children.Add(rm);
            rows.Add(Row(wc.Type, null, ctl));
        }
        p.Children.Add(Card(rows.ToArray()));
    }

    void PageMenuBar(StackPanel p)
    {
        AppCfg c = Cfg.Data;
        p.Children.Add(Head("Widget bar"));
        p.Children.Add(Card(
            Row("Show menu bar", "A slim glass bar across the top of the desktop. Maximized windows cover it.", Toggle(c.MenuBar, delegate(bool v) { c.MenuBar = v; Cfg.Touch(); WidgetManager.ApplyMenuBar(); })),
            Row("Battery", null, Toggle(c.MenuBattery, delegate(bool v) { c.MenuBattery = v; Cfg.Touch(); })),
            Row("Date & time", null, Toggle(c.MenuDate, delegate(bool v) { c.MenuDate = v; Cfg.Touch(); }))));
    }

    void PageWidgets(StackPanel p)
    {
        status = T("Click a widget to add it to your desktop", 12, FontWeights.Normal, Theme.Sub);
        status.Margin = new Thickness(6, 0, 0, 14);
        p.Children.Add(status);
        TextBlock st = status;
        string[] sizes = { "Small", "Medium", "Large" };
        foreach (string type in WF.Types)
        {
            p.Children.Add(Head(type));
            Border back = new Border(); back.CornerRadius = new CornerRadius(12); back.Padding = new Thickness(12, 12, 4, 4); back.Margin = new Thickness(0, 0, 0, 18);
            LinearGradientBrush gb = new LinearGradientBrush();
            gb.StartPoint = new Point(0, 0); gb.EndPoint = new Point(1, 1);
            gb.GradientStops.Add(new GradientStop(Color.FromRgb(46, 110, 200), 0));
            gb.GradientStops.Add(new GradientStop(Color.FromRgb(140, 70, 190), 0.55));
            gb.GradientStops.Add(new GradientStop(Color.FromRgb(240, 130, 90), 1));
            back.Background = gb;
            WrapPanel wp = new WrapPanel();
            for (int s = 0; s < 2; s++)
                for (int m = 0; m < 3; m++)
                {
                    string t = type; int style = s; Mode mode = (Mode)m;
                    Border cell = new Border(); cell.Margin = new Thickness(0, 0, 8, 8); cell.Cursor = Cursors.Hand; cell.Background = Brushes.Transparent;
                    cell.Child = WF.Preview(t, style, mode, 0.5);
                    cell.ToolTip = WF.StyleName(t, style) + " · " + sizes[m];
                    cell.MouseLeftButtonUp += delegate
                    {
                        WidgetManager.AddNew(t, style, mode);
                        st.Text = "Added " + t + " (" + WF.StyleName(t, style) + ", " + sizes[(int)mode] + ")";
                    };
                    wp.Children.Add(cell);
                }
            back.Child = wp;
            p.Children.Add(back);
        }
    }
}
