using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// ---------- Windows account ----------
static class UserInfo
{
    public static string DisplayName()
    {
        try
        {
            StringBuilder sb = new StringBuilder(256); uint n = 256;
            if (Native.GetUserNameEx(3, sb, ref n) && sb.Length > 0) return sb.ToString();
        }
        catch { }
        return Environment.UserName;
    }

    public static ImageSource Picture()
    {
        try
        {
            string sid = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\" + sid))
            {
                if (k != null)
                {
                    string[] names = { "Image448", "Image240", "Image192", "Image96", "Image64", "Image40", "Image32" };
                    foreach (string nm in names)
                    {
                        string p = k.GetValue(nm) as string;
                        ImageSource s = Load(p);
                        if (s != null) return s;
                    }
                }
            }
        }
        catch { }
        string pd = Environment.GetEnvironmentVariable("ProgramData");
        if (pd != null)
        {
            string[] fb = { "user-192.png", "user.png" };
            foreach (string f in fb)
            {
                ImageSource s = Load(System.IO.Path.Combine(pd, @"Microsoft\User Account Pictures\" + f));
                if (s != null) return s;
            }
        }
        return null;
    }

    static ImageSource Load(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;
            BitmapImage b = new BitmapImage();
            b.BeginInit();
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.UriSource = new Uri(path);
            b.DecodePixelWidth = 128;
            b.EndInit(); b.Freeze();
            return b;
        }
        catch { return null; }
    }
}

// ---------- Battery (laptop + Bluetooth devices) ----------
class BatItem { public string Name; public int Percent; public bool Charging; public bool Bt; }

static class Bat
{
    public static volatile List<BatItem> Items = new List<BatItem>();
    static Timer timer;

    public static void Start()
    {
        Refresh();
        timer = new Timer(delegate { Refresh(); }, null, 30000, 30000);
    }

    public static void Refresh()
    {
        List<BatItem> l = new List<BatItem>();
        try { l.AddRange(Bluetooth()); } catch (Exception e) { Cfg.LogError("bt: " + e.Message); }
        try { BatItem pc = Pc(); if (pc != null) l.Add(pc); } catch { }
        Items = l;
    }

    static BatItem Pc()
    {
        Native.SYSTEM_POWER_STATUS s;
        if (!Native.GetSystemPowerStatus(out s)) return null;
        if ((s.BatteryFlag & 128) != 0 || s.BatteryLifePercent > 100) return null;
        BatItem b = new BatItem();
        b.Name = "This PC"; b.Percent = s.BatteryLifePercent;
        b.Charging = (s.BatteryFlag & 8) != 0 || (s.ACLineStatus == 1 && s.BatteryLifePercent < 100 && false);
        return b;
    }

    static readonly Native.DEVPROPKEY KeyBattery = Key("104EA319-6EE2-4701-BD47-8DDBF425BBE5", 2);
    static readonly Native.DEVPROPKEY KeyFriendly = Key("a45c254e-df1c-4efd-8020-67d146a850e0", 14);
    static readonly Native.DEVPROPKEY KeyName = Key("b725f130-47ef-101a-a5f1-02608c9eebac", 10);
    static readonly Native.DEVPROPKEY KeyConnected = Key("83DA6326-97A6-4088-9453-A1923F573B29", 15);

    static Native.DEVPROPKEY Key(string g, uint pid)
    {
        Native.DEVPROPKEY k = new Native.DEVPROPKEY(); k.fmtid = new Guid(g); k.pid = pid; return k;
    }

    static byte[] Prop(IntPtr set, ref Native.SP_DEVINFO_DATA d, Native.DEVPROPKEY key)
    {
        byte[] buf = new byte[512]; uint type, req;
        if (!Native.SetupDiGetDevicePropertyW(set, ref d, ref key, out type, buf, (uint)buf.Length, out req, 0)) return null;
        byte[] r = new byte[Math.Min(req, (uint)buf.Length)];
        Array.Copy(buf, r, r.Length);
        return r;
    }

    static List<BatItem> Bluetooth()
    {
        List<BatItem> res = new List<BatItem>();
        IntPtr set = Native.SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, 0x2 | 0x4); // PRESENT | ALLCLASSES
        if (set == IntPtr.Zero || set == new IntPtr(-1)) return res;
        try
        {
            for (uint i = 0; ; i++)
            {
                Native.SP_DEVINFO_DATA d = new Native.SP_DEVINFO_DATA();
                d.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.SP_DEVINFO_DATA));
                if (!Native.SetupDiEnumDeviceInfo(set, i, ref d)) break;
                byte[] lv = Prop(set, ref d, KeyBattery);
                if (lv == null || lv.Length < 1) continue;
                int pct = lv[0];
                if (pct > 100) continue;
                byte[] conn = Prop(set, ref d, KeyConnected);
                if (conn != null && conn.Length >= 1 && conn[0] == 0) continue; // known to be disconnected
                byte[] nm = Prop(set, ref d, KeyFriendly);
                if (nm == null) nm = Prop(set, ref d, KeyName);
                string name = nm == null ? "Bluetooth device" : Encoding.Unicode.GetString(nm).TrimEnd('\0');
                int cut = name.IndexOf(" Hands-Free", StringComparison.OrdinalIgnoreCase);
                if (cut > 0) name = name.Substring(0, cut);
                bool dup = false;
                foreach (BatItem o in res) if (o.Name == name) dup = true;
                if (dup) continue;
                BatItem b = new BatItem(); b.Name = name; b.Percent = pct; b.Bt = true;
                res.Add(b);
            }
        }
        finally { Native.SetupDiDestroyDeviceInfoList(set); }
        return res;
    }
}

// ---------- Weather (Open-Meteo) ----------
class WeatherData
{
    public double Temp; public int Code; public bool IsDay; public string City;
    public List<double> DMax = new List<double>(), DMin = new List<double>();
    public List<int> DCode = new List<int>();
    public List<string> DName = new List<string>();
    public List<double> HT = new List<double>();
    public List<int> HC = new List<int>();
    public List<string> HL = new List<string>();
}

static class Weather
{
    public static volatile WeatherData Data;
    public static volatile string Status = "Loading...";
    static Timer timer;
    static bool located;
    static readonly object sync = new object();

    public static void Start()
    {
        try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
        timer = new Timer(delegate { Refresh(); }, null, 0, 15 * 60 * 1000);
    }

    public static void RefreshAsync() { ThreadPool.QueueUserWorkItem(delegate { Refresh(); }); }

    static string Get(string url)
    {
        using (WebClient wc = new WebClient())
        {
            wc.Encoding = Encoding.UTF8;
            wc.Headers["User-Agent"] = "MacLookLike";
            return wc.DownloadString(url);
        }
    }

    static double D(object o) { return o == null ? 0 : Convert.ToDouble(o, CultureInfo.InvariantCulture); }
    static string S(object o) { return o == null ? "" : o.ToString(); }
    static List<object> L(object o)
    {
        List<object> r = new List<object>();
        System.Collections.IEnumerable e = o as System.Collections.IEnumerable;
        if (e != null && !(o is string)) foreach (object x in e) r.Add(x);
        return r;
    }

    public static void Refresh()
    {
        lock (sync)
        {
            try
            {
                AppCfg c = Cfg.Data;
                JavaScriptSerializer ser = new JavaScriptSerializer();
                if (!c.AutoLoc) located = false;
                if (c.AutoLoc && !located)
                {
                    try
                    {
                        Dictionary<string, object> j = (Dictionary<string, object>)ser.DeserializeObject(Get("https://ipwho.is/"));
                        if (j.ContainsKey("latitude") && j["latitude"] != null)
                        {
                            c.Lat = D(j["latitude"]); c.Lon = D(j["longitude"]);
                            c.City = S(j.ContainsKey("city") ? j["city"] : null);
                            located = true; Cfg.SaveNow();
                        }
                    }
                    catch { }
                }
                if (c.Lat == 0 && c.Lon == 0) { Status = "No location"; return; }
                string url = "https://api.open-meteo.com/v1/forecast?latitude=" + c.Lat.ToString(CultureInfo.InvariantCulture)
                    + "&longitude=" + c.Lon.ToString(CultureInfo.InvariantCulture)
                    + "&current=temperature_2m,weather_code,is_day"
                    + "&hourly=temperature_2m,weather_code"
                    + "&daily=weather_code,temperature_2m_max,temperature_2m_min"
                    + "&timezone=auto&forecast_days=6";
                Dictionary<string, object> root = (Dictionary<string, object>)ser.DeserializeObject(Get(url));
                Dictionary<string, object> cur = (Dictionary<string, object>)root["current"];
                Dictionary<string, object> daily = (Dictionary<string, object>)root["daily"];
                Dictionary<string, object> hourly = (Dictionary<string, object>)root["hourly"];
                WeatherData w = new WeatherData();
                w.City = c.City; w.Temp = D(cur["temperature_2m"]); w.Code = (int)D(cur["weather_code"]); w.IsDay = D(cur["is_day"]) > 0;
                List<object> dt = L(daily["time"]), dc = L(daily["weather_code"]), dx = L(daily["temperature_2m_max"]), dn = L(daily["temperature_2m_min"]);
                for (int i = 0; i < dt.Count; i++)
                {
                    DateTime day = DateTime.ParseExact(S(dt[i]), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    w.DName.Add(i == 0 ? "Today" : day.ToString("ddd"));
                    w.DCode.Add((int)D(dc[i])); w.DMax.Add(D(dx[i])); w.DMin.Add(D(dn[i]));
                }
                string now = S(cur["time"]);
                string hourKey = now.Length >= 13 ? now.Substring(0, 13) : now;
                List<object> ht = L(hourly["time"]), hv = L(hourly["temperature_2m"]), hc = L(hourly["weather_code"]);
                int start = 0;
                for (int i = 0; i < ht.Count; i++) { if (string.CompareOrdinal(S(ht[i]), hourKey) > 0) { start = i; break; } }
                for (int i = start; i < ht.Count && w.HT.Count < 6; i++)
                {
                    string ts = S(ht[i]);
                    w.HL.Add(ts.Length >= 13 ? ts.Substring(11, 2) : ts);
                    w.HT.Add(D(hv[i])); w.HC.Add((int)D(hc[i]));
                }
                Data = w; Status = "OK";
            }
            catch (Exception e) { Status = "Offline"; Cfg.LogError("weather: " + e.Message); }
        }
    }

    public static bool Search(string q, out string msg)
    {
        try
        {
            string url = "https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(q) + "&count=1&language=en&format=json";
            Dictionary<string, object> j = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(Get(url));
            List<object> r = j.ContainsKey("results") ? L(j["results"]) : new List<object>();
            if (r.Count == 0) { msg = "No place found for \"" + q + "\"."; return false; }
            Dictionary<string, object> p = (Dictionary<string, object>)r[0];
            AppCfg c = Cfg.Data;
            c.AutoLoc = false; c.Lat = D(p["latitude"]); c.Lon = D(p["longitude"]); c.City = S(p["name"]);
            Cfg.SaveNow();
            msg = "Showing weather for " + S(p["name"]) + (p.ContainsKey("country") ? ", " + S(p["country"]) : "") + ".";
            return true;
        }
        catch { msg = "Couldn't reach the weather service."; return false; }
    }
}
