use chrono::{Datelike, Duration, Local, NaiveDate, NaiveDateTime, Timelike};
use serde::{Deserialize, Serialize};
use std::fs;
use std::io::Write;
use std::path::{Path, PathBuf};
use std::process::Command;
use std::sync::OnceLock;
use std::time::Duration as StdDuration;

pub const MAGIC: &[u8; 16] = b"DJS-TIME-V1-CFG!";
const RUN_KEY: &str = r"HKCU\Software\Microsoft\Windows\CurrentVersion\Run";
const RUN_VALUE: &str = "ShiguangCountdownRust";

#[derive(Serialize, Deserialize, Clone, Debug)]
#[serde(default)]
pub struct Event {
    pub id: String,
    pub title: String,
    pub target: String,
    pub mode: i32,
    pub accent: String,
    pub created: String,
    pub repeat_yearly: bool,
}

impl Default for Event {
    fn default() -> Self {
        Self {
            id: String::new(),
            title: String::new(),
            target: String::new(),
            mode: 0,
            accent: "#7C9CFF".to_string(),
            created: String::new(),
            repeat_yearly: false,
        }
    }
}

#[derive(Serialize, Deserialize, Clone, Debug)]
#[serde(default)]
pub struct Config {
    pub version: i32,
    pub events: Vec<Event>,
    pub selected_id: String,
    pub win_x: i32,
    pub win_y: i32,
    pub win_w: i32,
    pub win_h: i32,
    pub has_win_pos: bool,
    pub has_win_size: bool,
    pub top_most: bool,
    pub theme_mode: i32,
    pub auto_start: bool,
    pub language: i32,
    pub mini: bool,
    pub mini_x: i32,
    pub mini_y: i32,
    pub has_mini_pos: bool,
    pub saved_at: String,
}

impl Default for Config {
    fn default() -> Self {
        Self {
            version: 2,
            events: Vec::new(),
            selected_id: String::new(),
            win_x: -1,
            win_y: -1,
            win_w: 1100,
            win_h: 760,
            has_win_pos: false,
            has_win_size: false,
            top_most: false,
            theme_mode: 0,
            auto_start: false,
            language: 0,
            mini: false,
            mini_x: -1,
            mini_y: -1,
            has_mini_pos: false,
            saved_at: String::new(),
        }
    }
}

impl Config {
    pub fn sanitize(&mut self) {
        if self.theme_mode < 0 || self.theme_mode > 2 {
            self.theme_mode = 0;
        }
        if self.language != 1 {
            self.language = 0;
        }
        if !self.has_win_size || self.win_w < 760 || self.win_w > 8000 || self.win_h < 520 || self.win_h > 6000 {
            self.has_win_size = false;
            self.win_w = 1100;
            self.win_h = 760;
        }
        for e in &mut self.events {
            if e.id.is_empty() {
                e.id = new_id();
            }
            if e.title.is_empty() {
                e.title = "未命名日子".to_string();
            }
            if e.accent.is_empty() || !e.accent.starts_with('#') {
                e.accent = "#7C9CFF".to_string();
            }
            if !(0..=2).contains(&e.mode) {
                e.mode = 0;
            }
            if parse_dt(&e.target).is_none() {
                e.target = fmt_dt(Local::now().naive_local().date().and_hms_opt(9, 0, 0).unwrap());
            }
            if parse_dt(&e.created).is_none() {
                e.created = fmt_dt(now());
            }
        }
        if !self.events.iter().any(|e| e.id == self.selected_id) {
            self.selected_id = self.events.first().map(|e| e.id.clone()).unwrap_or_default();
        }
    }
}

pub fn now() -> NaiveDateTime {
    Local::now().naive_local()
}

pub fn new_id() -> String {
    let nanos = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_nanos())
        .unwrap_or(0);
    format!("{:x}{:x}", nanos, std::process::id())
}

pub fn parse_dt(s: &str) -> Option<NaiveDateTime> {
    NaiveDateTime::parse_from_str(s, "%Y-%m-%d %H:%M:%S").ok()
}

pub fn fmt_dt(t: NaiveDateTime) -> String {
    t.format("%Y-%m-%d %H:%M:%S").to_string()
}

pub fn now_string() -> String {
    Local::now().format("%Y-%m-%d %H:%M:%S%.3f").to_string()
}

pub fn days_in_month(year: i32, month: u32) -> u32 {
    let (ny, nm) = if month == 12 { (year + 1, 1) } else { (year, month + 1) };
    NaiveDate::from_ymd_opt(ny, nm, 1)
        .unwrap()
        .pred_opt()
        .unwrap()
        .day()
}

pub fn occurrence(original: NaiveDateTime, year: i32) -> NaiveDateTime {
    let month = original.month();
    let day = original.day().min(days_in_month(year, month));
    NaiveDate::from_ymd_opt(year, month, day)
        .unwrap()
        .and_hms_opt(original.hour(), original.minute(), original.second())
        .unwrap()
}

pub fn next_occurrence(original: NaiveDateTime, now: NaiveDateTime) -> NaiveDateTime {
    let mut c = occurrence(original, now.year());
    if c <= now {
        c = occurrence(original, now.year() + 1);
    }
    c
}

pub fn previous_occurrence(original: NaiveDateTime, now: NaiveDateTime) -> NaiveDateTime {
    let mut c = occurrence(original, now.year());
    if c >= now {
        c = occurrence(original, now.year() - 1);
    }
    c
}

impl Event {
    pub fn target_dt(&self) -> NaiveDateTime {
        parse_dt(&self.target).unwrap_or_else(now)
    }
    pub fn created_dt(&self) -> NaiveDateTime {
        parse_dt(&self.created).unwrap_or_else(now)
    }
    pub fn effective_target(&self, now: NaiveDateTime) -> NaiveDateTime {
        if self.repeat_yearly && self.mode != 2 {
            next_occurrence(self.target_dt(), now)
        } else {
            self.target_dt()
        }
    }
    pub fn progress_start(&self, now: NaiveDateTime) -> NaiveDateTime {
        if self.repeat_yearly && self.mode != 2 {
            let orig = self.target_dt();
            let prev = previous_occurrence(orig, now);
            if prev < orig {
                orig
            } else {
                prev
            }
        } else {
            self.created_dt()
        }
    }
    pub fn anniversary_number(&self, occurrence: NaiveDateTime) -> i32 {
        (occurrence.year() - self.target_dt().year()).max(0)
    }
}

pub fn split_duration(target: NaiveDateTime, now: NaiveDateTime) -> (bool, i64, i64, i64, i64, i64) {
    let diff = target.signed_duration_since(now);
    let future = diff.num_seconds() > 0;
    let total = if future {
        diff.num_seconds()
    } else {
        (-diff).num_seconds()
    };
    (future, total / 86400, (total % 86400) / 3600, (total % 3600) / 60, total % 60, total)
}

pub fn crc32(data: &[u8]) -> u32 {
    let mut table = [0u32; 256];
    for i in 0..256u32 {
        let mut c = i;
        for _ in 0..8 {
            c = if c & 1 != 0 { 0xEDB8_8320 ^ (c >> 1) } else { c >> 1 };
        }
        table[i as usize] = c;
    }
    let mut crc = 0xFFFF_FFFFu32;
    for b in data {
        crc = table[((crc ^ *b as u32) & 0xFF) as usize] ^ (crc >> 8);
    }
    crc ^ 0xFFFF_FFFF
}

pub fn find_trailer(bytes: &[u8]) -> Option<usize> {
    if bytes.len() < 24 {
        return None;
    }
    let mut i = bytes.len().saturating_sub(24);
    loop {
        if bytes[i] == MAGIC[0] && &bytes[i..i + 16] == MAGIC {
            let len = i32::from_le_bytes(bytes[i + 16..i + 20].try_into().ok()?) as i64;
            if len >= 2 && i + 24 + len as usize == bytes.len() {
                let stored = u32::from_le_bytes(bytes[i + 20 + len as usize..i + 24 + len as usize].try_into().ok()?);
                if crc32(&bytes[i + 20..i + 20 + len as usize]) == stored {
                    return Some(i);
                }
            }
        }
        if i == 0 {
            break;
        }
        i -= 1;
    }
    None
}

pub fn strip_trailer(bytes: &[u8]) -> Vec<u8> {
    match find_trailer(bytes) {
        Some(i) => bytes[..i].to_vec(),
        None => bytes.to_vec(),
    }
}

pub fn append_trailer(base: &[u8], payload: &[u8]) -> Vec<u8> {
    let mut out = Vec::with_capacity(base.len() + 24 + payload.len());
    out.extend_from_slice(base);
    out.extend_from_slice(MAGIC);
    out.extend_from_slice(&(payload.len() as i32).to_le_bytes());
    out.extend_from_slice(payload);
    out.extend_from_slice(&crc32(payload).to_le_bytes());
    out
}

pub fn temp_folder() -> PathBuf {
    std::env::temp_dir().join("ShiguangCountdownRust")
}

pub fn hash_path(path: &Path) -> String {
    let p = path.to_string_lossy().to_lowercase();
    let mut h: u64 = 14695981039346656037;
    for b in p.bytes() {
        h ^= b as u64;
        h = h.wrapping_mul(1099511628211);
    }
    format!("{:016x}", h)
}

pub struct Store {
    pub exe: PathBuf,
    pub pending: PathBuf,
    pub hash8: String,
    pub cfg: Config,
    pub dirty: bool,
}

impl Store {
    pub fn open() -> Self {
        let exe = std::env::current_exe().unwrap_or_else(|_| PathBuf::from("shiguang.exe"));
        let hash = hash_path(&exe);
        let pending = temp_folder().join(format!("{}.pending.json", hash));
        let embedded = fs::read(&exe).ok().and_then(|b| {
            find_trailer(&b).map(|i| {
                let len = i32::from_le_bytes(b[i + 16..i + 20].try_into().unwrap()) as usize;
                String::from_utf8_lossy(&b[i + 20..i + 20 + len]).to_string()
            })
        });
        let pending_json = fs::read_to_string(&pending).ok();
        let mut cfg = Config::default();
        let mut dirty = false;
        let pick = match (&embedded, &pending_json) {
            (Some(e), Some(p)) => {
                let ec = serde_json::from_str::<Config>(e).ok();
                let pc = serde_json::from_str::<Config>(p).ok();
                match (ec, pc) {
                    (Some(ec), Some(pc)) => {
                        if pc.saved_at >= ec.saved_at { dirty = true; pc } else { ec }
                    }
                    (Some(ec), None) => ec,
                    (None, Some(pc)) => { dirty = true; pc }
                    (None, None) => Config::default(),
                }
            }
            (Some(e), None) => serde_json::from_str::<Config>(e).unwrap_or_default(),
            (None, Some(p)) => {
                dirty = true;
                serde_json::from_str::<Config>(p).unwrap_or_default()
            }
            (None, None) => Config::default(),
        };
        cfg = pick;
        cfg.sanitize();
        let store = Self {
            exe,
            pending,
            hash8: hash[..8].to_string(),
            cfg,
            dirty,
        };
        store.cleanup_old_patchers();
        store
    }

    pub fn cleanup_old_patchers(&self) {
        let dir = temp_folder();
        if let Ok(rd) = fs::read_dir(&dir) {
            let prefix = format!("patch-{}-", self.hash8);
            for ent in rd.flatten() {
                let name = ent.file_name().to_string_lossy().to_string();
                if name.starts_with(&prefix) && name.ends_with(".exe") {
                    let _ = fs::remove_file(ent.path());
                }
            }
        }
    }

    pub fn save(&mut self) {
        self.cfg.saved_at = now_string();
        self.dirty = true;
        let _ = fs::create_dir_all(temp_folder());
        if let Ok(json) = serde_json::to_string(&self.cfg) {
            let _ = write_file(&self.pending, json.as_bytes());
        }
    }

    pub fn finish_exit(&mut self) {
        self.save();
        if !self.pending.exists() {
            return;
        }
        let _ = fs::create_dir_all(temp_folder());
        let stamp = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .map(|d| d.as_millis())
            .unwrap_or(0);
        let patcher = temp_folder().join(format!(
            "patch-{}-{}-{}.exe",
            self.hash8,
            std::process::id(),
            stamp
        ));
        let current = std::env::current_exe().unwrap_or_else(|_| self.exe.clone());
        if fs::copy(&current, &patcher).is_ok() {
            let _ = Command::new(&patcher)
                .arg("--patch")
                .arg(&self.exe)
                .arg(&self.pending)
                .creation_flags(0x08000000)
                .spawn();
        }
    }
}

fn write_file(path: &Path, data: &[u8]) -> std::io::Result<()> {
    let mut f = fs::File::create(path)?;
    f.write_all(data)?;
    f.sync_all()
}

#[cfg(target_os = "windows")]
use std::os::windows::process::CommandExt;
#[cfg(target_os = "windows")]
use std::os::windows::ffi::OsStrExt;

#[cfg(target_os = "windows")]
#[link(name = "kernel32")]
extern "system" {
    fn MoveFileExW(existing: *const u16, new: *const u16, flags: u32) -> i32;
}

#[cfg(target_os = "windows")]
fn wide(path: &Path) -> Vec<u16> {
    path.as_os_str().encode_wide().chain(std::iter::once(0)).collect()
}

#[cfg(target_os = "windows")]
fn replace_file(from: &Path, to: &Path) -> bool {
    unsafe {
        MoveFileExW(
            wide(from).as_ptr(),
            wide(to).as_ptr(),
            1 | 8, // REPLACE_EXISTING | WRITE_THROUGH
        ) != 0
    }
}

#[cfg(not(target_os = "windows"))]
fn replace_file(from: &Path, to: &Path) -> bool {
    fs::rename(from, to).is_ok()
}

pub fn run_patcher(target: &Path, pending: &Path) {
    let started = std::time::Instant::now();
    while started.elapsed() < StdDuration::from_secs(30) {
        if !target.exists() || !pending.exists() {
            std::thread::sleep(StdDuration::from_millis(250));
            continue;
        }
        let json = match fs::read_to_string(pending) {
            Ok(s) => s,
            Err(_) => {
                std::thread::sleep(StdDuration::from_millis(250));
                continue;
            }
        };
        if serde_json::from_str::<Config>(&json).is_err() {
            return;
        }
        let old = match fs::read(target) {
            Ok(b) => b,
            Err(_) => {
                std::thread::sleep(StdDuration::from_millis(250));
                continue;
            }
        };
        let base = strip_trailer(&old);
        let new = append_trailer(&base, json.as_bytes());
        let dir = target.parent().unwrap_or(Path::new("."));
        let tmp = dir.join(format!(
            ".{}.tmp{}",
            target.file_name().unwrap_or_default().to_string_lossy(),
            std::process::id()
        ));
        if fs::write(&tmp, &new).is_err() {
            std::thread::sleep(StdDuration::from_millis(250));
            continue;
        }
        let mut ok = false;
        for _ in 0..40 {
            if replace_file(&tmp, target) {
                ok = true;
                break;
            }
            std::thread::sleep(StdDuration::from_millis(250));
        }
        let _ = fs::remove_file(&tmp);
        if ok {
            let _ = fs::remove_file(pending);
            return;
        }
        std::thread::sleep(StdDuration::from_millis(250));
    }
}

pub fn autostart_set(exe: &Path, enabled: bool) -> bool {
    let exe_str = exe.to_string_lossy().to_string();
    let mut cmd = Command::new("reg.exe");
    if enabled {
        cmd.args([
            "add",
            RUN_KEY,
            "/v",
            RUN_VALUE,
            "/t",
            "REG_SZ",
            "/d",
            &format!("\"{}\"", exe_str),
            "/f",
        ]);
    } else {
        cmd.args(["delete", RUN_KEY, "/v", RUN_VALUE, "/f"]);
    }
    #[cfg(target_os = "windows")]
    {
        cmd.creation_flags(0x08000000);
    }
    cmd.stdout(std::process::Stdio::null())
        .stderr(std::process::Stdio::null())
        .status()
        .map(|s| s.success())
        .unwrap_or(false)
}
