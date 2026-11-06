using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace Shiguang
{
    public enum AppTheme
    {
        System = 0,
        Light = 1,
        Dark = 2
    }

    internal static class ThemeManager
    {
        public static AppTheme Mode { get; private set; } = AppTheme.System;
        public static bool IsLight { get; private set; }

        public static void Apply(AppTheme mode)
        {
            Mode = mode;
            bool light = mode == AppTheme.Light || (mode == AppTheme.System && SystemUsesLight());
            IsLight = light;
            Ui.SetTheme(light);
        }

        public static bool SystemUsesLight()
        {
            try
            {
                object v = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", null);
                if (v is int) return (int)v != 0;
                if (v is long) return (long)v != 0;
                if (v is string) return (string)v != "0";
            }
            catch { }
            return false;
        }

        public static string Name(AppTheme mode)
        {
            if (mode == AppTheme.Light) return "浅色";
            if (mode == AppTheme.Dark) return "深色";
            return "跟随系统";
        }

        public static AppTheme Cycle(AppTheme mode)
        {
            if (mode == AppTheme.System) return AppTheme.Light;
            if (mode == AppTheme.Light) return AppTheme.Dark;
            return AppTheme.System;
        }
    }

    internal static class StartupManager
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "ShiguangCountdown";

        static string ShortcutPath
        {
            get
            {
                try
                {
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                        "Shiguang Countdown.lnk");
                }
                catch { return null; }
            }
        }

        static bool RegistryEnabled(string exePath)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    var v = key.GetValue(ValueName) as string;
                    if (string.IsNullOrEmpty(v)) return false;
                    return string.Equals(v.Trim().Trim('"'), exePath, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        public static bool IsEnabled(string exePath)
        {
            if (RegistryEnabled(exePath)) return true;
            try
            {
                var lnk = ShortcutPath;
                return lnk != null && File.Exists(lnk);
            }
            catch { return false; }
        }

        public static bool SetEnabled(string exePath, bool enabled)
        {
            bool registryOk = false;
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key != null)
                    {
                        if (enabled)
                            key.SetValue(ValueName, "\"" + exePath + "\"", RegistryValueKind.String);
                        else
                            key.DeleteValue(ValueName, false);
                        registryOk = true;
                    }
                }
            }
            catch { }

            if (enabled && registryOk)
            {
                DeleteShortcut();
                return true;
            }
            if (!enabled && registryOk)
            {
                DeleteShortcut();
                return true;
            }

            // Fallback for locked-down environments: a normal Startup shortcut.
            if (enabled) return CreateShortcut(exePath);
            DeleteShortcut();
            return !IsEnabled(exePath);
        }

        public static void Sync(bool enabled, string exePath)
        {
            try
            {
                if (enabled) SetEnabled(exePath, true);
                else if (IsEnabled(exePath) || File.Exists(ShortcutPath ?? ""))
                    SetEnabled(exePath, false);
            }
            catch { }
        }

        static bool CreateShortcut(string exePath)
        {
            try
            {
                string lnk = ShortcutPath;
                if (lnk == null) return false;
                object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                if (shell == null) return false;
                object shortcut = shell.GetType().InvokeMember("CreateShortcut",
                    BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                if (shortcut == null) return false;
                shortcut.GetType().InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exePath });
                shortcut.GetType().InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut,
                    new object[] { Path.GetDirectoryName(exePath) ?? "" });
                shortcut.GetType().InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut,
                    new object[] { exePath + ",0" });
                shortcut.GetType().InvokeMember("Description", BindingFlags.SetProperty, null, shortcut,
                    new object[] { "Shiguang Countdown" });
                shortcut.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                return File.Exists(lnk);
            }
            catch { return false; }
        }

        static void DeleteShortcut()
        {
            try
            {
                string lnk = ShortcutPath;
                if (lnk != null && File.Exists(lnk)) File.Delete(lnk);
            }
            catch { }
        }
    }
}
