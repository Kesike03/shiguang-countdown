using System;
using System.Globalization;

namespace Shiguang
{
    internal static class L10n
    {
        public static int Lang = 0; // 0 = 中文, 1 = English
        public static bool En { get { return Lang == 1; } }

        public static string T(string zh, string en) { return En ? en : zh; }

        static CultureInfo Zh { get { return CultureInfo.GetCultureInfo("zh-CN"); } }
        static CultureInfo EnUs { get { return CultureInfo.GetCultureInfo("en-US"); } }

        public static string Human(DateTime t)
        {
            return En
                ? t.ToString("MMM d, yyyy · ddd HH:mm", EnUs)
                : t.ToString("yyyy年M月d日 dddd HH:mm", Zh);
        }

        public static string ShortDate(DateTime t)
        {
            return En ? t.ToString("MMM d, yyyy HH:mm", EnUs) : t.ToString("yyyy.MM.dd HH:mm", CultureInfo.InvariantCulture);
        }

        public static string ShortDay(DateTime t)
        {
            return En ? t.ToString("MMM d, yyyy", EnUs) : t.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
        }

        public static string YearLabel(int year)
        {
            return En ? year.ToString(CultureInfo.InvariantCulture) : year + "年";
        }

        public static string MonthLabel(DateTime month)
        {
            return En ? month.ToString("MMM", EnUs) : month.Month + "月";
        }

        public static string WeekHeader(int mondayIndex)
        {
            if (!En) return new[] { "一", "二", "三", "四", "五", "六", "日" }[mondayIndex];
            return new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }[mondayIndex];
        }

        public static string DayWord(int days)
        {
            if (!En) return "天";
            return days == 1 ? "day" : "days";
        }

        public static string DaysUnit(bool future)
        {
            if (!En) return future ? "天后" : "天前";
            return future ? "days" : "days ago";
        }

        public static string Today { get { return T("今天", "Today"); } }
        public static string Tomorrow { get { return T("明天", "Tomorrow"); } }
        public static string Yesterday { get { return T("昨天", "Yesterday"); } }

        public static string Anniversary(int n)
        {
            if (!En) return n <= 0 ? "首次" : "第 " + n + " 个周年";
            if (n <= 0) return "first";
            return Ordinal(n) + " anniversary";
        }

        static string Ordinal(int n)
        {
            int mod100 = n % 100;
            if (mod100 >= 11 && mod100 <= 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }

        public static string ModeName(int mode)
        {
            if (mode == 1) return T("倒数", "Countdown");
            if (mode == 2) return T("正计时", "Count up");
            return T("自动", "Auto");
        }

        public static string ModeDescription(int mode)
        {
            if (mode == 1)
                return T("始终倒数；过期后显示已过去的时间", "Always counts down; past shows elapsed time");
            if (mode == 2)
                return T("从该时刻起累计，适合纪念日", "Counts up from this moment");
            return T("未到显示倒数，过去显示已经历时间", "Future counts down, past counts up");
        }

        public static string ThemeName(AppTheme mode)
        {
            if (mode == AppTheme.Light) return T("浅色", "Light");
            if (mode == AppTheme.Dark) return T("深色", "Dark");
            return T("跟随系统", "System");
        }

        public static string EventCount(int n)
        {
            return En ? n + (n == 1 ? " day" : " days") : n + " 个";
        }

        public static string DeleteTitle { get { return T("删除日子", "Delete day"); } }

        public static string DeleteConfirm(string title)
        {
            return En
                ? "Delete \"" + title + "\"?\nThis cannot be undone."
                : "确定删除「" + title + "」吗？\n此操作无法撤销。";
        }

        public static string LangButton { get { return En ? "中" : "EN"; } }
    }
}
