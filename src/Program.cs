using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace MacLookLike
{
    internal sealed class TrayContext : ApplicationContext
    {
        readonly Manager manager = new Manager();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ToolStripMenuItem toggle;
        Icon icon;

        public TrayContext()
        {
            icon = MakeIcon();
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem title = new ToolStripMenuItem("MacLookLike");
            title.Enabled = false;
            toggle = new ToolStripMenuItem("Enabled");
            toggle.Checked = true;
            toggle.Click += delegate { Toggle(); };
            ToolStripMenuItem exit = new ToolStripMenuItem("Restore windows && Exit");
            exit.Click += delegate { ExitThread(); };
            menu.Items.Add(title);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(toggle);
            menu.Items.Add(exit);

            tray.Icon = icon;
            tray.Text = "MacLookLike";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.DoubleClick += delegate { Toggle(); };

            manager.SetEnabled(true);
        }

        void Toggle()
        {
            manager.SetEnabled(!manager.Enabled);
            toggle.Checked = manager.Enabled;
        }

        static Icon MakeIcon()
        {
            using (Bitmap bmp = new Bitmap(16, 16))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    Color[] cs = { Color.FromArgb(255, 95, 87), Color.FromArgb(254, 188, 46), Color.FromArgb(40, 200, 64) };
                    for (int i = 0; i < 3; i++)
                        using (SolidBrush b = new SolidBrush(cs[i])) g.FillEllipse(b, 0.5f + i * 5f, 4.5f, 5f, 5f);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        protected override void ExitThreadCore()
        {
            try { manager.Dispose(); } catch { }
            tray.Visible = false;
            tray.Dispose();
            if (icon != null) icon.Dispose();
            base.ExitThreadCore();
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try { N.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { } // per-monitor v2 (manifest also sets it)

            foreach (string a in args)
            {
                if (string.Equals(a, "--restore", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "/restore", StringComparison.OrdinalIgnoreCase))
                {
                    Manager.RestoreAll();
                    return 0;
                }
            }

            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\MacLookLike.SingleInstance", out created))
            {
                if (!created) return 0;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate { };
                AppDomain.CurrentDomain.UnhandledException += delegate { try { Manager.RestoreAll(); } catch { } };

                Application.Run(new TrayContext());
                try { Manager.RestoreAll(); } catch { } // safety net on exit
            }
            return 0;
        }
    }
}
