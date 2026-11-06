using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace Shiguang
{
    public class EventItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Target { get; set; }   // local time, yyyy-MM-dd HH:mm:ss
        public int Mode { get; set; }        // 0 auto, 1 countdown, 2 count up
        public string Accent { get; set; }   // #RRGGBB
        public string Created { get; set; }
        public string Note { get; set; }
        public bool RepeatYearly { get; set; }
    }

    public class AppConfig
    {
        public int Version { get; set; }
        public List<EventItem> Events { get; set; }
        public string SelectedId { get; set; }
        public int WinX { get; set; }
        public int WinY { get; set; }
        public int WinW { get; set; }
        public int WinH { get; set; }
        public bool HasWinSize { get; set; }
        public bool HasWinPos { get; set; }
        public bool TopMost { get; set; }
        public int ThemeMode { get; set; }
        public bool AutoStart { get; set; }
        public int Language { get; set; }
        public bool Mini { get; set; }
        public int MiniX { get; set; }
        public int MiniY { get; set; }
        public bool HasMiniPos { get; set; }
        public string SavedAt { get; set; }

        public AppConfig()
        {
            Version = 2;
            Events = new List<EventItem>();
            SelectedId = "";
            WinX = -1;
            WinY = -1;
            WinW = 1100;
            WinH = 720;
            HasWinSize = false;
            HasWinPos = false;
            TopMost = false;
            ThemeMode = 0;
            AutoStart = false;
            Language = 0;
            Mini = false;
            MiniX = -1;
            MiniY = -1;
            HasMiniPos = false;
            SavedAt = "";
        }
    }

    public static class EventUtil
    {
        public const string Fmt = "yyyy-MM-dd HH:mm:ss";

        public static string ToIso(DateTime t) { return t.ToString(Fmt, CultureInfo.InvariantCulture); }

        public static DateTime Parse(string s, DateTime fallback)
        {
            DateTime t;
            if (!string.IsNullOrEmpty(s) &&
                DateTime.TryParseExact(s, Fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
                return t;
            if (!string.IsNullOrEmpty(s) &&
                DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
                return t;
            return fallback;
        }

        public static DateTime TargetOf(EventItem e) { return Parse(e == null ? null : e.Target, DateTime.Now.Date.AddDays(1)); }
        public static DateTime CreatedOf(EventItem e) { return Parse(e == null ? null : e.Created, DateTime.Now); }

        public static Color AccentOf(EventItem e, int index)
        {
            Color fallback = Ui.HexColor(Ui.Palette[((index % Ui.Palette.Length) + Ui.Palette.Length) % Ui.Palette.Length], Ui.Brand);
            return e == null ? fallback : Ui.HexColor(e.Accent, fallback);
        }

        public static string NewId() { return Guid.NewGuid().ToString("N"); }

        public static string ModeName(int mode) { return L10n.ModeName(mode); }
        public static string ModeDescription(int mode) { return L10n.ModeDescription(mode); }
        public static string Human(DateTime t) { return L10n.Human(t); }
        public static string ShortDate(DateTime t) { return L10n.ShortDate(t); }
        public static string ShortDay(DateTime t) { return L10n.ShortDay(t); }

        public static EventItem Clone(EventItem e)
        {
            if (e == null) return null;
            return new EventItem
            {
                Id = e.Id,
                Title = e.Title,
                Target = e.Target,
                Mode = e.Mode,
                Accent = e.Accent,
                Created = e.Created,
                Note = e.Note,
                RepeatYearly = e.RepeatYearly
            };
        }

        static DateTime Occurrence(DateTime original, int year)
        {
            int day = Math.Min(original.Day, DateTime.DaysInMonth(year, original.Month));
            return new DateTime(year, original.Month, day, original.Hour, original.Minute, original.Second);
        }

        public static DateTime NextOccurrence(DateTime original, DateTime now)
        {
            var candidate = Occurrence(original, now.Year);
            if (candidate <= now) candidate = Occurrence(original, now.Year + 1);
            return candidate;
        }

        public static DateTime PreviousOccurrence(DateTime original, DateTime now)
        {
            var candidate = Occurrence(original, now.Year);
            if (candidate >= now) candidate = Occurrence(original, now.Year - 1);
            return candidate;
        }

        public static int AnniversaryNumber(DateTime original, DateTime occurrence)
        {
            int n = occurrence.Year - original.Year;
            return n < 0 ? 0 : n;
        }

        public static string AnniversaryLabel(DateTime original, DateTime occurrence)
        {
            int n = AnniversaryNumber(original, occurrence);
            return L10n.Anniversary(n);
        }

        /// <summary>The date that drives the big counter/timeline.</summary>
        public static DateTime EffectiveTarget(EventItem e, DateTime now)
        {
            var original = TargetOf(e);
            if (e != null && e.RepeatYearly && e.Mode != 2)
                return NextOccurrence(original, now);
            return original;
        }

        /// <summary>For repeating countdowns the timeline covers the current year between anniversaries.</summary>
        public static DateTime ProgressStart(EventItem e, DateTime now)
        {
            var original = TargetOf(e);
            if (e != null && e.RepeatYearly && e.Mode != 2)
            {
                var prev = PreviousOccurrence(original, now);
                if (prev < original) prev = original;
                return prev;
            }
            return CreatedOf(e);
        }

        // Days elapsed/remaining without the fractional day, used for compact labels.
        public static int WholeDays(DateTime target, DateTime now)
        {
            double days = (target - now).TotalDays;
            if (days >= 0) return (int)Math.Ceiling(days - 1e-9);
            return (int)Math.Floor(-days + 1e-9);
        }
    }

    internal static class Crc32
    {
        static readonly uint[] Table = BuildTable();

        static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = ((c & 1) != 0) ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
                table[i] = c;
            }
            return table;
        }

        public static uint Compute(byte[] data, int offset, int length)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < length; i++)
                crc = Table[(crc ^ data[offset + i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }

    /// <summary>
    /// Stores the whole application state inside the exe itself: a trailer appended
    /// after the PE image. While the app is running Windows locks its own exe, so
    /// the state is first written to a pending file in %TEMP% and a copy of the exe
    /// (the patcher) applies it right after the app exits.
    /// </summary>
    internal sealed class ConfigStore
    {
        public const string MagicText = "DJS-TIME-V1-CFG!";
        public const int HeaderSize = 16;
        const int Overhead = 20 + 4; // magic + length + crc32

        public string ExePath { get; private set; }
        public string PendingPath { get; private set; }
        public AppConfig Config { get; private set; }
        public bool Dirty { get; set; }
        public bool FirstRun { get; private set; }

        string _hash;

        public static string TempFolder
        {
            get { return Path.Combine(Path.GetTempPath(), "ShiguangCountdown"); }
        }

        public static string HashPath(string path)
        {
            ulong h = 14695981039346656037UL;
            string p = Path.GetFullPath(path).ToLowerInvariant();
            for (int i = 0; i < p.Length; i++)
            {
                h ^= (byte)(p[i] & 0xFF);
                h *= 1099511628211UL;
                h ^= (byte)(p[i] >> 8);
                h *= 1099511628211UL;
            }
            return h.ToString("x16");
        }

        public static ConfigStore Open(string exePath)
        {
            var store = new ConfigStore();
            store.ExePath = Path.GetFullPath(exePath);
            store._hash = HashPath(store.ExePath);
            store.PendingPath = Path.Combine(TempFolder, store._hash + ".pending.json");
            store.WaitForPatcher();
            store.CleanupOldPatchers();

            AppConfig embedded = ReadEmbedded(store.ExePath);
            AppConfig pending = ReadPending(store.PendingPath);

            AppConfig chosen = embedded;
            bool dirty = false;
            if (pending != null)
            {
                bool pendingWins = embedded == null ||
                    string.CompareOrdinal(pending.SavedAt ?? "", embedded.SavedAt ?? "") >= 0;
                if (pendingWins)
                {
                    chosen = pending;
                    dirty = true;
                }
                else
                {
                    // A successful patch usually removes the pending file; if one is
                    // left over from an older version just clean it up.
                    try { File.Delete(store.PendingPath); } catch { }
                }
            }

            if (chosen == null)
            {
                chosen = new AppConfig();
                dirty = true;
                store.FirstRun = true;
            }

            Sanitize(chosen);
            store.Config = chosen;
            store.Dirty = dirty;
            return store;
        }

        static void Sanitize(AppConfig cfg)
        {
            if (cfg.Events == null) cfg.Events = new List<EventItem>();
            var clean = new List<EventItem>();
            foreach (var e in cfg.Events)
            {
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.Id)) e.Id = EventUtil.NewId();
                if (string.IsNullOrEmpty(e.Title)) e.Title = "未命名日子";
                if (string.IsNullOrEmpty(e.Accent)) e.Accent = Ui.Palette[0];
                else
                {
                    var c = Ui.HexColor(e.Accent, Color.Transparent);
                    if (c.A == 0) e.Accent = Ui.Palette[0];
                }
                if (e.Mode < 0 || e.Mode > 2) e.Mode = 0;
                DateTime t;
                if (string.IsNullOrEmpty(e.Target) ||
                    !DateTime.TryParseExact(e.Target, EventUtil.Fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
                {
                    e.Target = EventUtil.ToIso(DateTime.Now.Date.AddDays(1).AddHours(9));
                }
                DateTime created;
                if (string.IsNullOrEmpty(e.Created) ||
                    !DateTime.TryParseExact(e.Created, EventUtil.Fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out created))
                {
                    e.Created = EventUtil.ToIso(DateTime.Now);
                }
                clean.Add(e);
            }
            cfg.Events = clean;
            if (string.IsNullOrEmpty(cfg.SelectedId)) cfg.SelectedId = "";
            if (cfg.ThemeMode < 0 || cfg.ThemeMode > 2) cfg.ThemeMode = 0;
            if (!cfg.HasWinSize || cfg.WinW < 760 || cfg.WinW > 8000 || cfg.WinH < 520 || cfg.WinH > 6000)
            {
                cfg.HasWinSize = false;
                cfg.WinW = 1100;
                cfg.WinH = 720;
            }
            if (cfg.Language != 1) cfg.Language = 0;
            if (cfg.MiniX < -20000 || cfg.MiniX > 20000) cfg.MiniX = -1;
            if (cfg.MiniY < -20000 || cfg.MiniY > 20000) cfg.MiniY = -1;
        }

        void CleanupOldPatchers()
        {
            try
            {
                if (!Directory.Exists(TempFolder)) return;
                // Helpers are only needed right after the previous session closed.
                // After the patch mutex is free they can go, keeping %TEMP% tidy.
                foreach (var f in Directory.GetFiles(TempFolder, "patch-" + _hash.Substring(0, 8) + "-*.exe"))
                {
                    try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }

        void WaitForPatcher()
        {
            // If a previous session is just being patched, give it a moment so we
            // read the freshest embedded state instead of the old exe bytes.
            try
            {
                using (var m = new Mutex(false, "Local\\DJS_PATCH_" + _hash))
                {
                    try
                    {
                        if (m.WaitOne(2500)) m.ReleaseMutex();
                    }
                    catch { }
                }
            }
            catch { }
        }

        static string ApplicationExe()
        {
            try { return Process.GetCurrentProcess().MainModule.FileName; }
            catch { return System.Windows.Forms.Application.ExecutablePath; }
        }

        public void Save()
        {
            Dirty = true;
            try
            {
                Config.SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                string json = Serialize(Config);
                Directory.CreateDirectory(TempFolder);
                string tmp = PendingPath + "." + Guid.NewGuid().ToString("N").Substring(0, 6) + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                File.Copy(tmp, PendingPath, true);
                try { File.Delete(tmp); } catch { }
            }
            catch
            {
                // Keeping the state in memory is still better than crashing.
            }
        }

        public void PrepareExit()
        {
            try
            {
                if (!File.Exists(PendingPath)) return;
                string self = ApplicationExe();
                if (!string.Equals(Path.GetFullPath(self), ExePath, StringComparison.OrdinalIgnoreCase)) return;
                Directory.CreateDirectory(TempFolder);
                int pid = Process.GetCurrentProcess().Id;
                string patcher = Path.Combine(TempFolder,
                    "patch-" + _hash.Substring(0, 8) + "-" + pid + "-" +
                    Guid.NewGuid().ToString("N").Substring(0, 6) + ".exe");
                File.Copy(self, patcher, true);
                string args = "--patch \"" + ExePath + "\" \"" + PendingPath + "\" " + pid.ToString(CultureInfo.InvariantCulture);
                var psi = new ProcessStartInfo(patcher, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.WorkingDirectory = TempFolder;
                Process.Start(psi);
            }
            catch
            {
                // Pending file stays around; the next launch will load it again.
            }
        }

        // ------------------------------------------------------------------
        //  patcher mode (runs from a %TEMP% copy after the main app exits)
        // ------------------------------------------------------------------
        public static void RunPatcher(string target, string pending, string parentPidText)
        {
            string self = ApplicationExe();
            if (string.Equals(Path.GetFullPath(self), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                return; // never patch the currently running file

            Mutex mutex = null;
            bool owned = false;
            try
            {
                string hash = HashPath(target);
                try
                {
                    mutex = new Mutex(false, "Local\\DJS_PATCH_" + hash);
                    owned = mutex.WaitOne(60000);
                }
                catch { }

                int pid;
                if (int.TryParse(parentPidText, NumberStyles.Integer, CultureInfo.InvariantCulture, out pid))
                {
                    try
                    {
                        var p = Process.GetProcessById(pid);
                        p.WaitForExit(30000);
                    }
                    catch { }
                }

                if (!File.Exists(target) || !File.Exists(pending)) return;
                string json = File.ReadAllText(pending, Encoding.UTF8);
                if (string.IsNullOrEmpty(json)) return;
                if (Deserialize(json) == null) return;

                byte[] oldBytes = File.ReadAllBytes(target);
                byte[] baseBytes = StripTrailer(oldBytes);
                byte[] payload = Encoding.UTF8.GetBytes(json);
                byte[] newBytes = AppendTrailer(baseBytes, payload);

                string dir = Path.GetDirectoryName(target);
                string tmp = Path.Combine(dir, "." + Path.GetFileName(target) + "." +
                    Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp");
                File.WriteAllBytes(tmp, newBytes);

                bool ok = false;
                for (int attempt = 0; attempt < 25 && !ok; attempt++)
                {
                    try
                    {
                        if (File.Exists(target)) File.Replace(tmp, target, null, true);
                        else File.Move(tmp, target);
                        ok = true;
                    }
                    catch
                    {
                        Thread.Sleep(220);
                    }
                }
                if (!ok)
                {
                    try { File.Copy(tmp, target, true); ok = true; }
                    catch { }
                }
                if (ok)
                {
                    try { File.Delete(pending); } catch { }
                }
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
            catch
            {
                // keep pending file for the next session
            }
            finally
            {
                if (owned && mutex != null)
                {
                    try { mutex.ReleaseMutex(); } catch { }
                }
                if (mutex != null) { try { mutex.Dispose(); } catch { } }
            }
        }

        // ------------------------------------------------------------------
        //  exe trailer helpers
        // ------------------------------------------------------------------
        public static string Serialize(AppConfig cfg)
        {
            var ser = new JavaScriptSerializer();
            ser.MaxJsonLength = 16 * 1024 * 1024;
            ser.RecursionLimit = 100;
            return ser.Serialize(cfg);
        }

        static AppConfig Deserialize(string json)
        {
            try
            {
                var ser = new JavaScriptSerializer();
                ser.MaxJsonLength = 16 * 1024 * 1024;
                ser.RecursionLimit = 100;
                return ser.Deserialize<AppConfig>(json);
            }
            catch { return null; }
        }

        static AppConfig ReadPending(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var cfg = Deserialize(File.ReadAllText(path, Encoding.UTF8));
                if (cfg != null) Sanitize(cfg);
                return cfg;
            }
            catch { return null; }
        }

        static AppConfig ReadEmbedded(string exePath)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(exePath);
                int offset = FindTrailer(bytes);
                if (offset < 0) return null;
                int len = BitConverter.ToInt32(bytes, offset + 16);
                string json = Encoding.UTF8.GetString(bytes, offset + 20, len);
                var cfg = Deserialize(json);
                if (cfg != null) Sanitize(cfg);
                return cfg;
            }
            catch { return null; }
        }

        public static int FindTrailer(byte[] bytes)
        {
            if (bytes == null || bytes.Length < Overhead) return -1;
            int last = bytes.Length - Overhead;
            for (int i = last; i >= 0; i--)
            {
                if (bytes[i] != (byte)'D') continue;
                if (!MagicAt(bytes, i)) continue;
                if (i + Overhead > bytes.Length) continue;
                int len = BitConverter.ToInt32(bytes, i + 16);
                if (len < 2 || len > 16 * 1024 * 1024) continue;
                if (i + Overhead + len != bytes.Length) continue;
                uint stored = BitConverter.ToUInt32(bytes, i + 20 + len);
                if (Crc32.Compute(bytes, i + 20, len) != stored) continue;
                return i;
            }
            return -1;
        }

        static bool MagicAt(byte[] b, int i)
        {
            for (int k = 0; k < HeaderSize; k++)
                if (b[i + k] != (byte)MagicText[k]) return false;
            return true;
        }

        public static byte[] StripTrailer(byte[] bytes)
        {
            int offset = FindTrailer(bytes);
            if (offset < 0) return bytes;
            var result = new byte[offset];
            Buffer.BlockCopy(bytes, 0, result, 0, offset);
            return result;
        }

        public static byte[] AppendTrailer(byte[] baseBytes, byte[] payload)
        {
            var result = new byte[baseBytes.Length + Overhead + payload.Length];
            Buffer.BlockCopy(baseBytes, 0, result, 0, baseBytes.Length);
            int p = baseBytes.Length;
            byte[] magic = Encoding.ASCII.GetBytes(MagicText);
            Buffer.BlockCopy(magic, 0, result, p, magic.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, result, p + 16, 4);
            Buffer.BlockCopy(payload, 0, result, p + 20, payload.Length);
            uint crc = Crc32.Compute(payload, 0, payload.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(crc), 0, result, p + 20 + payload.Length, 4);
            return result;
        }
    }
}
