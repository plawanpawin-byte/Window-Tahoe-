using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MacLookLike
{
    /// <summary>
    /// Tracks every eligible top-level window: hides its native caption buttons
    /// (by clearing WS_SYSMENU) and keeps a traffic-light overlay glued to its title bar.
    /// </summary>
    internal sealed class Manager : IDisposable
    {
        // Window property used as a persistent marker ("we cleared WS_SYSMENU on this window").
        // It lives in the target window, so a crashed instance can still be undone with --restore.
        const string Prop = "MacLookLike.Hidden";

        const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
        const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
        const uint EVENT_OBJECT_DESTROY = 0x8001;
        const uint EVENT_OBJECT_SHOW = 0x8002;
        const uint EVENT_OBJECT_HIDE = 0x8003;
        const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        const uint EVENT_OBJECT_CLOAKED = 0x8017;
        const uint EVENT_OBJECT_UNCLOAKED = 0x8018;
        const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        // Windows whose title bar is not a classic Win32 one: clearing WS_SYSMENU does nothing
        // useful (or breaks them), so they are left alone.
        static readonly HashSet<string> SkipClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ApplicationFrameWindow", "Windows.UI.Core.CoreWindow", "WinUIDesktopWin32WindowClass",
            "Chrome_WidgetWin_0", "Chrome_WidgetWin_1", "Chrome_WidgetWin_2", "MozillaWindowClass",
            "CASCADIA_HOSTING_WINDOW_CLASS", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman",
            "WorkerW", "XamlExplorerHostIslandWindow", "TaskManagerWindow", "ConsoleWindowClass"
        };

        sealed class Entry { public Overlay Ov; }

        readonly Dictionary<IntPtr, Entry> map = new Dictionary<IntPtr, Entry>();
        readonly HashSet<IntPtr> blocked = new HashSet<IntPtr>();
        readonly List<IntPtr> hooks = new List<IntPtr>();
        readonly N.WinEventProc proc;
        readonly Timer timer = new Timer();
        readonly uint myPid;
        int tick;
        bool enabled;
        bool disposed;

        public Manager()
        {
            myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            proc = OnEvent; // keep delegate alive
            timer.Interval = 120;
            timer.Tick += delegate { OnTimer(); };
        }

        public bool Enabled { get { return enabled; } }

        public void SetEnabled(bool on)
        {
            if (disposed || on == enabled) return;
            enabled = on;
            if (on)
            {
                Hook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND);
                Hook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND);
                Hook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_HIDE);
                Hook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE);
                Hook(EVENT_OBJECT_CLOAKED, EVENT_OBJECT_UNCLOAKED);
                Scan();
                timer.Start();
            }
            else
            {
                timer.Stop();
                foreach (IntPtr h in hooks) N.UnhookWinEvent(h);
                hooks.Clear();
                RemoveAll();
            }
        }

        void Hook(uint min, uint max)
        {
            IntPtr h = N.SetWinEventHook(min, max, IntPtr.Zero, proc, 0, 0, WINEVENT_SKIPOWNPROCESS);
            if (h != IntPtr.Zero) hooks.Add(h);
        }

        public void Dispose()
        {
            if (disposed) return;
            SetEnabled(false);
            disposed = true;
            timer.Dispose();
        }

        // ---- eligibility ----------------------------------------------------
        bool Eligible(IntPtr h)
        {
            if (!N.IsWindowVisible(h)) return false;
            uint st = N.GetStyle(h), ex = N.GetExStyle(h);
            if ((st & N.WS_CAPTION) != N.WS_CAPTION) return false;
            if ((st & N.WS_CHILD) != 0) return false;
            if ((ex & (N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE)) != 0) return false;
            bool ours = N.GetProp(h, Prop) != IntPtr.Zero;
            if ((st & N.WS_SYSMENU) == 0 && !ours) return false;
            uint pid;
            N.GetWindowThreadProcessId(h, out pid);
            if (pid == myPid) return false;
            if (N.IsCloaked(h)) return false;
            if (SkipClasses.Contains(N.ClassOf(h))) return false;
            return true;
        }

        // ---- scanning / events ---------------------------------------------
        void OnTimer()
        {
            try
            {
                tick++;
                if (tick % 4 == 0) Scan(); else UpdateAll();
            }
            catch (Exception ex) { N.Log("OnTimer: " + ex); }
        }

        void Scan()
        {
            HashSet<IntPtr> seen = new HashSet<IntPtr>();
            N.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                try { if (Eligible(h)) seen.Add(h); } catch { }
                return true;
            }, IntPtr.Zero);

            foreach (IntPtr h in seen)
                if (!map.ContainsKey(h) && !blocked.Contains(h)) Add(h);

            List<IntPtr> gone = new List<IntPtr>();
            foreach (IntPtr h in map.Keys) if (!seen.Contains(h)) gone.Add(h);
            foreach (IntPtr h in gone) Remove(h);

            blocked.RemoveWhere(delegate(IntPtr h) { return !N.IsWindow(h); });
            UpdateAll();
        }

        void OnEvent(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            try
            {
                if (!enabled) return;
                if (evt == EVENT_SYSTEM_FOREGROUND) { UpdateAll(); return; }
                if (idObject != 0 || idChild != 0 || hwnd == IntPtr.Zero) return; // OBJID_WINDOW only
                if (evt == EVENT_OBJECT_DESTROY) { Remove(hwnd); blocked.Remove(hwnd); return; }
                if (map.ContainsKey(hwnd)) { Update(hwnd); return; }
                if (evt == EVENT_OBJECT_SHOW || evt == EVENT_OBJECT_UNCLOAKED)
                {
                    if (!blocked.Contains(hwnd) && Eligible(hwnd)) Add(hwnd);
                }
            }
            catch (Exception ex) { N.Log("OnEvent: " + ex); }
        }

        // ---- add / remove / update -----------------------------------------
        void Add(IntPtr h)
        {
            bool ours = N.GetProp(h, Prop) != IntPtr.Zero;
            if (!ours)
            {
                N.SetStyle(h, N.GetStyle(h) & ~N.WS_SYSMENU);
                if ((N.GetStyle(h) & N.WS_SYSMENU) != 0) { blocked.Add(h); return; } // e.g. elevated window (UIPI)
                N.SetProp(h, Prop, new IntPtr(1));
                N.FrameChanged(h);
            }
            Entry e = new Entry();
            e.Ov = new Overlay(h);
            e.Ov.ButtonClicked += OnClick;
            map[h] = e;
            Update(h);
        }

        void Remove(IntPtr h)
        {
            Entry e;
            if (!map.TryGetValue(h, out e)) return;
            map.Remove(h);
            try { e.Ov.Dispose(); } catch { }
            Restore(h);
        }

        static void Restore(IntPtr h)
        {
            if (!N.IsWindow(h)) return;
            if (N.GetProp(h, Prop) == IntPtr.Zero) return;
            N.SetStyle(h, N.GetStyle(h) | N.WS_SYSMENU);
            N.RemoveProp(h, Prop);
            N.FrameChanged(h);
        }

        void RemoveAll()
        {
            List<IntPtr> all = new List<IntPtr>(map.Keys);
            foreach (IntPtr h in all) Remove(h);
        }

        /// <summary>Undo everything a (possibly crashed) instance did.</summary>
        public static int RestoreAll()
        {
            int n = 0;
            N.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (N.GetProp(h, Prop) != IntPtr.Zero) { Restore(h); n++; }
                return true;
            }, IntPtr.Zero);
            return n;
        }

        void UpdateAll()
        {
            List<IntPtr> all = new List<IntPtr>(map.Keys);
            foreach (IntPtr h in all) Update(h);
        }

        void Update(IntPtr h)
        {
            Entry e;
            if (!map.TryGetValue(h, out e)) return;
            if (!N.IsWindow(h)) { Remove(h); return; }

            uint st = N.GetStyle(h);
            if ((st & N.WS_SYSMENU) != 0) // app re-added it: hide again
            {
                N.SetStyle(h, st & ~N.WS_SYSMENU);
                N.FrameChanged(h);
                st = N.GetStyle(h);
            }

            bool visible = N.IsWindowVisible(h) && !N.IsIconic(h) && !N.IsCloaked(h);

            N.TITLEBARINFO tb = new N.TITLEBARINFO();
            tb.cbSize = Marshal.SizeOf(typeof(N.TITLEBARINFO));
            tb.rgstate = new int[6];
            bool ok = visible && N.GetTitleBarInfo(h, ref tb) && (tb.rgstate[0] & 0x8000) == 0 && tb.rcTitleBar.Height > 0;

            N.RECT ext = new N.RECT();
            if (ok && N.DwmGetRect(h, 9, out ext, Marshal.SizeOf(typeof(N.RECT))) != 0) ok = false;

            float s = N.ScaleOf(h);
            IntPtr fg = N.GetForegroundWindow();
            bool active = fg != IntPtr.Zero && N.GetAncestor(fg, 2) == h; // GA_ROOT

            e.Ov.Apply(ext.Left, tb.rcTitleBar.Top, tb.rcTitleBar.Height, s, active,
                (st & N.WS_MINIMIZEBOX) != 0, (st & N.WS_MAXIMIZEBOX) != 0, N.IsZoomed(h), ok);
        }

        // ---- button actions -------------------------------------------------
        void OnClick(IntPtr h, int button)
        {
            if (!N.IsWindow(h)) return;
            switch (button)
            {
                case Overlay.BtnClose:
                    N.PostMessage(h, N.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    break;
                case Overlay.BtnMin:
                    N.ShowWindowAsync(h, 6); // SW_MINIMIZE
                    break;
                case Overlay.BtnMax:
                    N.ShowWindowAsync(h, N.IsZoomed(h) ? 9 : 3); // SW_RESTORE : SW_MAXIMIZE
                    break;
            }
        }
    }
}
