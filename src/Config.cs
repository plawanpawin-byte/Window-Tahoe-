using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;

class WidgetCfg
{
    public string Id { get; set; }
    public string Type { get; set; }
    public int Style { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public string Text { get; set; }
}

class AppCfg
{
    public List<WidgetCfg> Widgets { get; set; }
    public bool Seeded { get; set; }
    public bool MenuBar { get; set; }
    public bool MenuBattery { get; set; }
    public bool MenuDate { get; set; }
    public bool Hour24 { get; set; }
    public bool Fahrenheit { get; set; }
    public bool AutoLoc { get; set; }
    public string City { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public int Tint { get; set; }          // 0 dark, 1 light, 2 clear
    public double Strength { get; set; }   // 0..1
    public bool Launch { get; set; }

    public AppCfg()
    {
        Widgets = new List<WidgetCfg>();
        MenuBattery = true; MenuDate = true; Hour24 = true; AutoLoc = true;
        City = ""; Strength = 0.35;
    }
}

static class Cfg
{
    public static AppCfg Data = new AppCfg();
    static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacLookLike");
    static readonly string FilePath = Path.Combine(Dir, "config.json");
    static DispatcherTimer timer;
    static readonly object sync = new object();

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                AppCfg d = new JavaScriptSerializer().Deserialize<AppCfg>(File.ReadAllText(FilePath));
                if (d != null) { Data = d; if (Data.Widgets == null) Data.Widgets = new List<WidgetCfg>(); }
            }
        }
        catch { }
        if (!Data.Seeded) { Seed(); Data.Seeded = true; SaveNow(); }
    }

    static WidgetCfg Make(string type, int style, double x, double y, double w, double h)
    {
        WidgetCfg c = new WidgetCfg();
        c.Id = Guid.NewGuid().ToString("N"); c.Type = type; c.Style = style;
        c.X = x; c.Y = y; c.W = w; c.H = h; c.Text = "";
        return c;
    }

    static void Seed()
    {
        Rect wa = SystemParameters.WorkArea;
        double x = wa.Right - 338 - 24, y = wa.Top + 24;
        Data.Widgets.Add(Make("Clock", 0, x, y, 338, 158));
        Data.Widgets.Add(Make("Calendar", 0, x, y + 174, 158, 158));
        Data.Widgets.Add(Make("Weather", 0, x + 180, y + 174, 158, 158));
        Data.Widgets.Add(Make("Battery", 0, x, y + 348, 338, 158));
    }

    public static WidgetCfg NewWidget(string type, int style, double w, double h)
    {
        Point p = FreeSpot(w, h);
        WidgetCfg c = Make(type, style, p.X, p.Y, w, h);
        Data.Widgets.Add(c);
        Touch();
        return c;
    }

    static Point FreeSpot(double w, double h)
    {
        Rect wa = SystemParameters.WorkArea;
        for (double x = wa.Right - w - 24; x >= wa.Left + 8; x -= 20)
            for (double y = wa.Top + 24; y + h <= wa.Bottom; y += 16)
            {
                Rect r = new Rect(x - 8, y - 8, w + 16, h + 16);
                bool hit = Data.Widgets.Any(delegate(WidgetCfg o) { return new Rect(o.X, o.Y, o.W, o.H).IntersectsWith(r); });
                if (!hit) return new Point(x, y);
            }
        return new Point(wa.Left + 60, wa.Top + 60);
    }

    public static void Touch()
    {
        if (timer == null)
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += delegate { timer.Stop(); SaveNow(); };
        }
        timer.Stop(); timer.Start();
    }

    public static void SaveNow()
    {
        lock (sync)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(Data));
            }
            catch { }
        }
    }

    public static void LogError(string s)
    {
        try { Directory.CreateDirectory(Dir); File.AppendAllText(Path.Combine(Dir, "error.log"), DateTime.Now + " " + s + "\r\n"); } catch { }
    }
}
