use chrono::{Datelike, NaiveDateTime, Timelike};

const MONTHS: [&str; 12] = [
    "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec",
];
const WEEK: [&str; 7] = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
const WEEK_ZH: [&str; 7] = ["星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"];
const WEEK_SHORT_ZH: [&str; 7] = ["一", "二", "三", "四", "五", "六", "日"];

pub fn tr(en: bool, zh: &str, en_s: &str) -> String {
    if en { en_s.to_string() } else { zh.to_string() }
}

pub fn date(d: NaiveDateTime, en: bool) -> String {
    let wi = d.weekday().num_days_from_monday() as usize;
    if en {
        format!(
            "{} {}, {} · {} {:02}:{:02}",
            MONTHS[d.month0() as usize],
            d.day(),
            d.year(),
            WEEK[wi],
            d.hour(),
            d.minute()
        )
    } else {
        format!(
            "{}年{}月{}日 {} {:02}:{:02}",
            d.year(),
            d.month(),
            d.day(),
            WEEK_ZH[wi],
            d.hour(),
            d.minute()
        )
    }
}

pub fn short_date(d: NaiveDateTime, en: bool) -> String {
    if en {
        format!(
            "{} {}, {} {:02}:{:02}",
            MONTHS[d.month0() as usize],
            d.day(),
            d.year(),
            d.hour(),
            d.minute()
        )
    } else {
        format!("{}.{:02}.{:02} {:02}:{:02}", d.year(), d.month(), d.day(), d.hour(), d.minute())
    }
}

pub fn short_day(d: NaiveDateTime, en: bool) -> String {
    if en {
        format!("{} {}, {}", MONTHS[d.month0() as usize], d.day(), d.year())
    } else {
        format!("{}.{:02}.{:02}", d.year(), d.month(), d.day())
    }
}

pub fn year_label(year: i32, en: bool) -> String {
    if en { format!("{}", year) } else { format!("{}年", year) }
}

pub fn month_label(d: NaiveDateTime, en: bool) -> String {
    if en { MONTHS[d.month0() as usize].to_string() } else { format!("{}月", d.month()) }
}

pub fn month_long(d: NaiveDateTime, en: bool) -> String {
    if en {
        format!("{} {}", MONTHS[d.month0() as usize], d.year())
    } else {
        format!("{}年{}月", d.year(), d.month())
    }
}

pub fn week_header(i: usize, en: bool) -> String {
    if en { WEEK[i].to_string() } else { WEEK_SHORT_ZH[i].to_string() }
}

pub fn day_word(days: i64, en: bool) -> String {
    if !en {
        "天".to_string()
    } else if days == 1 {
        "day".to_string()
    } else {
        "days".to_string()
    }
}

pub fn days_unit(future: bool, en: bool) -> String {
    if !en {
        if future { "天后".to_string() } else { "天前".to_string() }
    } else if future {
        "days".to_string()
    } else {
        "days ago".to_string()
    }
}

pub fn today(en: bool) -> String {
    tr(en, "今天", "Today")
}
pub fn tomorrow(en: bool) -> String {
    tr(en, "明天", "Tomorrow")
}
pub fn yesterday(en: bool) -> String {
    tr(en, "昨天", "Yesterday")
}

pub fn anniversary(n: i32, en: bool) -> String {
    if !en {
        if n <= 0 { "首次".to_string() } else { format!("第 {} 个周年", n) }
    } else if n <= 0 {
        "first".to_string()
    } else {
        format!("{} anniversary", ordinal(n))
    }
}

fn ordinal(n: i32) -> String {
    let m = n % 100;
    if (11..=13).contains(&m) {
        return format!("{}th", n);
    }
    match n % 10 {
        1 => format!("{}st", n),
        2 => format!("{}nd", n),
        3 => format!("{}rd", n),
        _ => format!("{}th", n),
    }
}

pub fn mode_name(mode: i32, en: bool) -> String {
    match mode {
        1 => tr(en, "倒数", "Countdown"),
        2 => tr(en, "正计时", "Count up"),
        _ => tr(en, "自动", "Auto"),
    }
}

pub fn mode_desc(mode: i32, en: bool) -> String {
    match mode {
        1 => tr(
            en,
            "始终倒数；过期后显示已过去的时间",
            "Always counts down; past shows elapsed time",
        ),
        2 => tr(en, "从该时刻起累计，适合纪念日", "Counts up from this moment"),
        _ => tr(en, "未到显示倒数，过去显示已经历时间", "Future counts down, past counts up"),
    }
}

pub fn theme_name(mode: i32, en: bool) -> String {
    match mode {
        1 => tr(en, "浅色", "Light"),
        2 => tr(en, "深色", "Dark"),
        _ => tr(en, "跟随系统", "System"),
    }
}

pub fn event_count(n: usize, en: bool) -> String {
    if en {
        format!("{} {}", n, if n == 1 { "day" } else { "days" })
    } else {
        format!("{} 个", n)
    }
}

pub fn placeholder(en: bool) -> String {
    tr(en, "例如：妈妈的生日", "e.g. A special day")
}

pub fn delete_title(en: bool) -> String {
    tr(en, "删除日子", "Delete day")
}

pub fn delete_confirm(title: &str, en: bool) -> String {
    if en {
        format!("Delete \"{}\"?\nThis cannot be undone.", title)
    } else {
        format!("确定删除「{}」吗？\n此操作无法撤销。", title)
    }
}
