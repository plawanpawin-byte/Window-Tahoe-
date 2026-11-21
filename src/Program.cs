using System;
using System.Threading;
using System.Windows;

public static class Program
{
    static Mutex single;
    static EventWaitHandle showEvt;
    static System.Windows.Forms.NotifyIcon tray;
    static Application app;

    [STAThread]
    public static int Main(string[] args)
    {
        bool background = Array.IndexOf(args, "--background") >= 0;
        bool created;
        single = new Mutex(true, "MacLookLike.Single.v2", out created);
        showEvt = new EventWaitHandle(false, EventResetMode.AutoReset, "MacLookLike.Show.v2");
        if (!created)
        {
            if (!background) showEvt.Set();
            return 0;
        }

        System.Globalization.CultureInfo en = new System.Globalization.CultureInfo("en-US");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = en;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = en;
        Thread.CurrentThread.CurrentCulture = en;
        Thread.CurrentThread.CurrentUICulture = en;

        app = new Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.DispatcherUnhandledException += delegate(object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Cfg.LogError(e.Exception.ToString());
            e.Handled = true;
        };

        Cfg.Load();
        Theme.Apply();
        Bat.Start();
        Weather.Start();
        WidgetManager.LoadAll();
        WidgetManager.ApplyMenuBar();
        MakeTray();

        Thread t = new Thread(delegate()
        {
            while (true)
            {
                showEvt.WaitOne();
                if (app == null) return;
                try { app.Dispatcher.BeginInvoke(new Action(delegate { SettingsWindow.Open(); })); } catch { return; }
            }
        });
        t.IsBackground = true;
        t.Start();

        if (!background) SettingsWindow.Open();
        app.Run();
        return 0;
    }

    static void MakeTray()
    {
        try
        {
            tray = new System.Windows.Forms.NotifyIcon();
            tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            tray.Text = "MacLookLike";
            System.Windows.Forms.ContextMenuStrip m = new System.Windows.Forms.ContextMenuStrip();
            m.Items.Add("Settings…", null, delegate { app.Dispatcher.BeginInvoke(new Action(delegate { SettingsWindow.Open(); })); });
            m.Items.Add("Quit MacLookLike", null, delegate { app.Dispatcher.BeginInvoke(new Action(Quit)); });
            tray.ContextMenuStrip = m;
            tray.MouseClick += delegate(object s, System.Windows.Forms.MouseEventArgs e)
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    app.Dispatcher.BeginInvoke(new Action(delegate { SettingsWindow.Open(); }));
            };
            tray.Visible = true;
        }
        catch (Exception ex) { Cfg.LogError(ex.ToString()); }
    }

    public static void Quit()
    {
        try { WidgetManager.CloseAll(); } catch { }
        try { Cfg.SaveNow(); } catch { }
        if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
        Application a = app; app = null;
        showEvt.Set();
        if (a != null) a.Shutdown();
    }
}
