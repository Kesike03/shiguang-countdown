use crate::config::{self, Config, Event, Store};
use crate::i18n;
use crate::ui::{self, Pal};
use chrono::{Datelike, Duration, Local, NaiveDate, NaiveDateTime, Timelike};
use eframe::egui::{self, Align2, Color32, FontId, Id, Key, Pos2, Rect, Sense, Stroke, StrokeKind, Vec2, ViewportCommand};
use std::path::Path;
use std::time::{Duration as StdDuration, Instant};

const ACCENTS: [&str; 8] = [
    "#7C9CFF", "#FF8A7A", "#FFC46B", "#6EE7B7",
    "#5BD4E8", "#C084FC", "#F472B6", "#A3E635",
];

#[derive(Clone, Copy, PartialEq, Eq)]
enum Hover {
    None,
    Theme,
    Mini,
    Expand,
    Pin,
    Min,
    Close,
    New,
    Edit,
    AutoStart,
    Language,
    Card(usize),
}

struct Editor {
    is_new: bool,
    id: String,
    created: String,
    title: String,
    date: NaiveDate,
    view_year: i32,
    view_month: u32,
    hour: u32,
    minute: u32,
    second: u32,
    mode: i32,
    repeat: bool,
    accent: Color32,
    just_opened: bool,
    close_requested: bool,
    save_requested: bool,
    delete_requested: bool,
    confirm_delete: bool,
}

pub struct App {
    store: Store,
    pal: Pal,
    en: bool,
    theme_mode: i32,
    selected: Option<usize>,
    hover: Hover,
    scroll: f32,
    mini: bool,
    top_most: bool,
    auto_start: bool,
    editor: Option<Editor>,
    last_outer: Option<Rect>,
    normal_size: Vec2,
    normal_pos: Pos2,
    last_bounds_save: Instant,
    last_theme_check: Instant,
    mouse: Pos2,
    title_focus: bool,
}

impl App {
    pub fn new(cc: &eframe::CreationContext<'_>, store: Store) -> Self {
        let cfg = store.cfg.clone();
        let pal = Pal::new(resolve_light(&cc.egui_ctx, cfg.theme_mode));
        let en = cfg.language == 1;
        let selected = cfg.events.iter().position(|e| e.id == cfg.selected_id);
        let mini = cfg.mini;
        let normal_size = Vec2::new(
            if cfg.has_win_size { cfg.win_w as f32 } else { 1100.0 },
            if cfg.has_win_size { cfg.win_h as f32 } else { 760.0 },
        );
        let normal_pos = if cfg.has_win_pos {
            Pos2::new(cfg.win_x as f32, cfg.win_y as f32)
        } else {
            Pos2::new(-1.0, -1.0)
        };
        let mut app = Self {
            store,
            pal,
            en,
            theme_mode: cfg.theme_mode,
            selected,
            hover: Hover::None,
            scroll: 0.0,
            mini,
            top_most: cfg.top_most,
            auto_start: cfg.auto_start,
            editor: None,
            last_outer: None,
            normal_size,
            normal_pos,
            last_bounds_save: Instant::now(),
            last_theme_check: Instant::now(),
            mouse: Pos2::ZERO,
            title_focus: false,
        };
        app.ensure_sample();
        app.store.save();

        if app.store.cfg.auto_start {
            config::autostart_set(&app.store.exe, true);
        }
        let ctx = &cc.egui_ctx;
        ctx.send_viewport_cmd(ViewportCommand::Title(app.window_title()));
        ctx.send_viewport_cmd(ViewportCommand::WindowLevel(if app.top_most {
            egui::WindowLevel::AlwaysOnTop
        } else {
            egui::WindowLevel::Normal
        }));
        if app.mini {
            app.send_mini_commands(ctx);
            if cfg.has_mini_pos {
                ctx.send_viewport_cmd(ViewportCommand::OuterPosition(Pos2::new(
                    cfg.mini_x as f32,
                    cfg.mini_y as f32,
                )));
            }
        } else {
            app.send_normal_commands(ctx);
            ctx.send_viewport_cmd(ViewportCommand::InnerSize(app.normal_size));
            if normal_pos.x >= 0.0 {
                ctx.send_viewport_cmd(ViewportCommand::OuterPosition(normal_pos));
            }
        }
        app
    }

    fn window_title(&self) -> String {
        i18n::tr(self.en, "时光倒数", "Shiguang Countdown")
    }

    fn ensure_sample(&mut self) {
        if self.store.cfg.events.is_empty() && self.store.cfg.saved_at.is_empty() {
            let now = config::now();
            let target = NaiveDate::from_ymd_opt(now.year() + 1, 1, 1)
                .unwrap()
                .and_hms_opt(0, 0, 0)
                .unwrap();
            let e = Event {
                id: config::new_id(),
                title: i18n::tr(self.en, "新的一年", "New Year"),
                target: config::fmt_dt(target),
                mode: 0,
                accent: "#FFC46B".to_string(),
                created: config::fmt_dt(now),
                repeat_yearly: false,
            };
            self.store.cfg.selected_id = e.id.clone();
            self.store.cfg.events.push(e);
            self.selected = Some(0);
            self.store.dirty = true;
        }
        if self.selected.is_none() && !self.store.cfg.events.is_empty() {
            self.selected = Some(0);
            self.store.cfg.selected_id = self.store.cfg.events[0].id.clone();
        }
    }

    fn send_normal_commands(&self, ctx: &egui::Context) {
        ctx.send_viewport_cmd(ViewportCommand::Resizable(true));
        ctx.send_viewport_cmd(ViewportCommand::MinInnerSize(Vec2::new(ui::MIN_W, ui::MIN_H)));
        ctx.send_viewport_cmd(ViewportCommand::MaxInnerSize(Vec2::new(10000.0, 10000.0)));
    }

    fn send_mini_commands(&self, ctx: &egui::Context) {
        ctx.send_viewport_cmd(ViewportCommand::Resizable(false));
        ctx.send_viewport_cmd(ViewportCommand::MinInnerSize(Vec2::new(ui::MINI_W, ui::MINI_H)));
        ctx.send_viewport_cmd(ViewportCommand::MaxInnerSize(Vec2::new(ui::MINI_W, ui::MINI_H)));
        ctx.send_viewport_cmd(ViewportCommand::InnerSize(Vec2::new(ui::MINI_W, ui::MINI_H)));
    }

    fn save(&mut self) {
        self.store.save();
    }

    fn selected_event(&self) -> Option<&Event> {
        self.selected.and_then(|i| self.store.cfg.events.get(i))
    }

    fn selected_accent(&self) -> Color32 {
        self.selected_event()
            .map(|e| ui::from_hex(&e.accent))
            .unwrap_or(self.pal.brand)
    }

    fn set_theme_mode(&mut self, mode: i32, ctx: &egui::Context) {
        self.theme_mode = mode;
        self.store.cfg.theme_mode = mode;
        self.pal = Pal::new(resolve_light(ctx, mode));
        self.save();
    }

    fn toggle_language(&mut self, ctx: &egui::Context) {
        self.en = !self.en;
        self.store.cfg.language = if self.en { 1 } else { 0 };
        ctx.send_viewport_cmd(ViewportCommand::Title(self.window_title()));
        self.save();
    }

    fn toggle_top_most(&mut self, ctx: &egui::Context) {
        self.top_most = !self.top_most;
        self.store.cfg.top_most = self.top_most;
        ctx.send_viewport_cmd(ViewportCommand::WindowLevel(if self.top_most {
            egui::WindowLevel::AlwaysOnTop
        } else {
            egui::WindowLevel::Normal
        }));
        self.save();
    }

    fn toggle_auto_start(&mut self) {
        let want = !self.auto_start;
        if config::autostart_set(&self.store.exe, want) {
            self.auto_start = want;
            self.store.cfg.auto_start = want;
            self.save();
        }
    }

    fn enter_mini(&mut self, ctx: &egui::Context) {
        if self.mini {
            return;
        }
        if let Some(r) = self.current_outer(ctx) {
            self.normal_pos = r.min;
            self.normal_size = r.size();
            self.store.cfg.win_x = r.min.x as i32;
            self.store.cfg.win_y = r.min.y as i32;
            self.store.cfg.win_w = r.width() as i32;
            self.store.cfg.win_h = r.height() as i32;
            self.store.cfg.has_win_pos = true;
            self.store.cfg.has_win_size = true;
        }
        self.mini = true;
        self.store.cfg.mini = true;
        self.send_mini_commands(ctx);
        let monitor = ctx.input(|i| i.viewport().monitor_size).unwrap_or(Vec2::new(1920.0, 1080.0));
        let pos = Pos2::new(
            (monitor.x - ui::MINI_W - 28.0).max(0.0),
            28.0,
        );
        ctx.send_viewport_cmd(ViewportCommand::OuterPosition(pos));
        self.store.cfg.mini_x = pos.x as i32;
        self.store.cfg.mini_y = pos.y as i32;
        self.store.cfg.has_mini_pos = true;
        self.save();
    }

    fn exit_mini(&mut self, ctx: &egui::Context) {
        if !self.mini {
            return;
        }
        if let Some(r) = self.current_outer(ctx) {
            self.store.cfg.mini_x = r.min.x as i32;
            self.store.cfg.mini_y = r.min.y as i32;
            self.store.cfg.has_mini_pos = true;
        }
        self.mini = false;
        self.store.cfg.mini = false;
        self.send_normal_commands(ctx);
        ctx.send_viewport_cmd(ViewportCommand::InnerSize(self.normal_size));
        if self.normal_pos.x >= 0.0 {
            ctx.send_viewport_cmd(ViewportCommand::OuterPosition(self.normal_pos));
        }
        self.save();
    }

    fn current_outer(&self, ctx: &egui::Context) -> Option<Rect> {
        ctx.input(|i| i.viewport().outer_rect)
    }

    fn capture_bounds(&mut self, ctx: &egui::Context) {
        if let Some(r) = self.current_outer(ctx) {
            if self.mini {
                self.store.cfg.mini_x = r.min.x as i32;
                self.store.cfg.mini_y = r.min.y as i32;
                self.store.cfg.has_mini_pos = true;
            } else if r.width() > 200.0 && r.height() > 200.0 {
                self.store.cfg.win_x = r.min.x as i32;
                self.store.cfg.win_y = r.min.y as i32;
                self.store.cfg.win_w = r.width() as i32;
                self.store.cfg.win_h = r.height() as i32;
                self.store.cfg.has_win_pos = true;
                self.store.cfg.has_win_size = true;
            }
        }
    }

    fn open_new(&mut self) {
        let now = config::now();
        let target = (now.date() + Duration::days(1))
            .and_hms_opt(9, 0, 0)
            .unwrap();
        self.editor = Some(Editor {
            is_new: true,
            id: config::new_id(),
            created: config::fmt_dt(now),
            title: String::new(),
            date: target.date(),
            view_year: target.year(),
            view_month: target.month(),
            hour: target.hour(),
            minute: target.minute(),
            second: target.second(),
            mode: 0,
            repeat: false,
            accent: ui::from_hex(ACCENTS[self.store.cfg.events.len() % ACCENTS.len()]),
            just_opened: true,
            close_requested: false,
            save_requested: false,
            delete_requested: false,
            confirm_delete: false,
        });
        self.title_focus = true;
    }

    fn open_edit(&mut self) {
        if let Some(i) = self.selected {
            let e = self.store.cfg.events[i].clone();
            let t = e.target_dt();
            self.editor = Some(Editor {
                is_new: false,
                id: e.id,
                created: e.created,
                title: e.title,
                date: t.date(),
                view_year: t.year(),
                view_month: t.month(),
                hour: t.hour(),
                minute: t.minute(),
                second: t.second(),
                mode: e.mode,
                repeat: e.repeat_yearly,
                accent: ui::from_hex(&e.accent),
                just_opened: true,
                close_requested: false,
                save_requested: false,
                delete_requested: false,
                confirm_delete: false,
            });
            self.title_focus = true;
        }
    }

    fn commit_editor(&mut self, ed: &Editor) {
        let title = if ed.title.trim().is_empty() {
            i18n::tr(self.en, "未命名日子", "Untitled day").to_string()
        } else {
            ed.title.trim().chars().take(48).collect()
        };
        let target = ed
            .date
            .and_hms_opt(ed.hour, ed.minute, ed.second)
            .unwrap_or_else(config::now);
        let accent = format!(
            "#{:02X}{:02X}{:02X}",
            ed.accent.r(),
            ed.accent.g(),
            ed.accent.b()
        );
        let item = Event {
            id: ed.id.clone(),
            title,
            target: config::fmt_dt(target),
            mode: ed.mode,
            accent,
            created: if ed.created.is_empty() {
                config::fmt_dt(config::now())
            } else {
                ed.created.clone()
            },
            repeat_yearly: ed.repeat,
        };
        if ed.is_new {
            self.store.cfg.events.insert(0, item);
            self.selected = Some(0);
        } else if let Some(idx) = self.store.cfg.events.iter().position(|e| e.id == ed.id) {
            self.store.cfg.events[idx] = item;
            self.selected = Some(idx);
        }
        if let Some(i) = self.selected {
            self.store.cfg.selected_id = self.store.cfg.events[i].id.clone();
        }
        self.scroll = 0.0;
        self.save();
    }

    fn delete_selected(&mut self) {
        if let Some(i) = self.selected {
            if i < self.store.cfg.events.len() {
                self.store.cfg.events.remove(i);
                self.selected = if self.store.cfg.events.is_empty() {
                    None
                } else {
                    Some(i.min(self.store.cfg.events.len() - 1))
                };
                self.store.cfg.selected_id = self
                    .selected
                    .and_then(|j| self.store.cfg.events.get(j))
                    .map(|e| e.id.clone())
                    .unwrap_or_default();
                self.save();
            }
        }
    }

    fn select(&mut self, i: usize) {
        if i < self.store.cfg.events.len() {
            self.selected = Some(i);
            self.store.cfg.selected_id = self.store.cfg.events[i].id.clone();
            self.save();
        }
    }
}

impl App {
    fn top_button(&self, full: Rect, n: i32) -> Rect {
        Rect::from_min_size(
            Pos2::new(full.right() - 56.0 - 36.0 * n as f32, full.top() + 18.0),
            Vec2::new(32.0, 28.0),
        )
    }

    fn mini_button(&self, full: Rect, n: i32) -> Rect {
        // n: 0 close, 1 expand, 2 pin
        let x = match n {
            0 => full.right() - 44.0,
            1 => full.right() - 78.0,
            _ => full.right() - 112.0,
        };
        Rect::from_min_size(Pos2::new(x, full.top() + 14.0), Vec2::new(26.0, 26.0))
    }

    fn language_rect(&self, full: Rect) -> Rect {
        Rect::from_min_size(Pos2::new(full.left() + 246.0, full.top() + 28.0), Vec2::new(38.0, 22.0))
    }

    fn list_rect(&self, full: Rect) -> Rect {
        let h = (full.height() - 136.0 - 126.0).max(120.0);
        Rect::from_min_size(Pos2::new(full.left() + 14.0, full.top() + 136.0), Vec2::new(278.0, h))
    }

    fn auto_row_rect(&self, full: Rect) -> Rect {
        Rect::from_min_size(
            Pos2::new(full.left() + 22.0, full.bottom() - 112.0),
            Vec2::new(262.0, 26.0),
        )
    }

    fn new_rect(&self, full: Rect) -> Rect {
        Rect::from_min_size(
            Pos2::new(full.left() + 22.0, full.bottom() - 82.0),
            Vec2::new(262.0, 50.0),
        )
    }

    fn edit_rect(&self, full: Rect) -> Rect {
        Rect::from_min_size(
            Pos2::new(full.right() - 42.0 - 90.0, full.top() + 80.0),
            Vec2::new(90.0, 34.0),
        )
    }

    fn hero_rect(&self, full: Rect) -> Rect {
        let x = full.left() + ui::SIDEBAR + 42.0;
        let y = full.top() + 148.0;
        let w = (full.width() - x + full.left() - 42.0).max(200.0);
        let h = (full.height() - 148.0 - 84.0).max(300.0);
        Rect::from_min_size(Pos2::new(x, y), Vec2::new(w, h))
    }

    fn card_rect(&self, full: Rect, i: usize) -> Rect {
        let list = self.list_rect(full);
        Rect::from_min_size(
            Pos2::new(list.left(), list.top() + i as f32 * 84.0 - self.scroll),
            Vec2::new(list.width(), 76.0),
        )
    }

    fn scroll_max(&self, full: Rect) -> f32 {
        let total = self.store.cfg.events.len() as f32 * 84.0 - 8.0;
        (total - self.list_rect(full).height()).max(0.0)
    }

    fn edge_at(&self, full: Rect, pos: Pos2) -> Option<egui::ResizeDirection> {
        if self.mini {
            return None;
        }
        let t = 6.0;
        let l = pos.x <= full.left() + t;
        let r = pos.x >= full.right() - t;
        let top = pos.y <= full.top() + t;
        let b = pos.y >= full.bottom() - t;
        if top && l { Some(egui::ResizeDirection::NorthWest) }
        else if top && r { Some(egui::ResizeDirection::NorthEast) }
        else if b && l { Some(egui::ResizeDirection::SouthWest) }
        else if b && r { Some(egui::ResizeDirection::SouthEast) }
        else if l { Some(egui::ResizeDirection::West) }
        else if r { Some(egui::ResizeDirection::East) }
        else if top { Some(egui::ResizeDirection::North) }
        else if b { Some(egui::ResizeDirection::South) }
        else { None }
    }

    fn edge_cursor(dir: egui::ResizeDirection) -> egui::CursorIcon {
        use egui::ResizeDirection::*;
        match dir {
            North | South => egui::CursorIcon::ResizeVertical,
            East | West => egui::CursorIcon::ResizeHorizontal,
            NorthEast | SouthWest => egui::CursorIcon::ResizeNeSw,
            NorthWest | SouthEast => egui::CursorIcon::ResizeNwSe,
        }
    }

    fn card_hit(&self, full: Rect, pos: Pos2) -> Option<usize> {
        let list = self.list_rect(full);
        if !list.contains(pos) {
            return None;
        }
        for i in 0..self.store.cfg.events.len() {
            let r = self.card_rect(full, i);
            if r.bottom() < list.top() || r.top() > list.bottom() {
                continue;
            }
            if r.contains(pos) {
                return Some(i);
            }
        }
        None
    }

    fn in_header(&self, full: Rect, pos: Pos2) -> bool {
        pos.y < full.top() + 64.0 && pos.x >= full.left() + ui::SIDEBAR
    }

    fn draw_main(&mut self, ui: &mut egui::Ui, full: Rect) {
        let ctx = ui.ctx().clone();
        let p = ui.painter().clone();
        ui::gradient(&p, full, self.pal.bg_top, self.pal.bg_bottom);

        if self.mini {
            self.handle_mini_input(&ctx, full);
            self.draw_mini(&ctx, &p, full);
            return;
        }

        self.handle_normal_input(&ctx, full);
        self.draw_sidebar(&ctx, &p, full);
        self.draw_main_panel(&ctx, &p, full);


    }

    fn handle_normal_input(&mut self, ctx: &egui::Context, full: Rect) {
        let pointer = ctx.input(|i| i.pointer.hover_pos()).unwrap_or(self.mouse);
        self.mouse = pointer;
        let clicked = ctx.input(|i| i.pointer.primary_clicked());
        let pressed = ctx.input(|i| i.pointer.primary_pressed());

        if let Some(dir) = self.edge_at(full, pointer) {
            ctx.set_cursor_icon(Self::edge_cursor(dir));
            if pressed {
                ctx.send_viewport_cmd(ViewportCommand::BeginResize(dir));
                return;
            }
        }

        let btns = [
            (Hover::Theme, self.top_button(full, 4)),
            (Hover::Mini, self.top_button(full, 3)),
            (Hover::Pin, self.top_button(full, 2)),
            (Hover::Min, self.top_button(full, 1)),
            (Hover::Close, self.top_button(full, 0)),
        ];
        let lang = self.language_rect(full);
        let newr = self.new_rect(full);
        let autor = self.auto_row_rect(full);
        let editr = self.edit_rect(full);

        let mut hover = Hover::None;
        for (h, r) in btns.iter() {
            if r.contains(pointer) {
                hover = *h;
            }
        }
        if lang.contains(pointer) { hover = Hover::Language; }
        if newr.contains(pointer) { hover = Hover::New; }
        if autor.contains(pointer) { hover = Hover::AutoStart; }
        if self.selected.is_some() && editr.contains(pointer) { hover = Hover::Edit; }
        if hover == Hover::None {
            if let Some(i) = self.card_hit(full, pointer) { hover = Hover::Card(i); }
        }
        if hover == Hover::None && self.in_header(full, pointer) {
            ctx.set_cursor_icon(egui::CursorIcon::Grab);
        } else if hover != Hover::None {
            ctx.set_cursor_icon(egui::CursorIcon::PointingHand);
        }
        self.hover = hover;

        if pressed && hover == Hover::None && self.in_header(full, pointer) {
            ctx.send_viewport_cmd(ViewportCommand::StartDrag);
            return;
        }

        if !clicked {
            let scroll = ctx.input(|i| i.raw_scroll_delta.y);
            if scroll.abs() > 0.1 {
                let list = self.list_rect(full);
                if list.contains(pointer) {
                    self.scroll = (self.scroll - scroll * 0.8).clamp(0.0, self.scroll_max(full));
                    self.store.cfg.selected_id = self
                        .selected
                        .and_then(|i| self.store.cfg.events.get(i))
                        .map(|e| e.id.clone())
                        .unwrap_or_default();
                }
            }
            return;
        }

        match hover {
            Hover::Close => { ctx.send_viewport_cmd(ViewportCommand::Close); }
            Hover::Min => { ctx.send_viewport_cmd(ViewportCommand::Minimized(true)); }
            Hover::Pin => self.toggle_top_most(ctx),
            Hover::Theme => {
                let next = (self.theme_mode + 1) % 3;
                self.set_theme_mode(next, ctx);
            }
            Hover::Mini => self.enter_mini(ctx),
            Hover::Language => self.toggle_language(ctx),
            Hover::New => self.open_new(),
            Hover::Edit => self.open_edit(),
            Hover::AutoStart => self.toggle_auto_start(),
            Hover::Card(i) => self.select(i),
            _ => {}
        }
    }

    fn handle_mini_input(&mut self, ctx: &egui::Context, full: Rect) {
        let pointer = ctx.input(|i| i.pointer.hover_pos()).unwrap_or(self.mouse);
        self.mouse = pointer;
        let clicked = ctx.input(|i| i.pointer.primary_clicked());
        let pressed = ctx.input(|i| i.pointer.primary_pressed());
        let close = self.mini_button(full, 0);
        let expand = self.mini_button(full, 1);
        let pin = self.mini_button(full, 2);
        let mut hover = Hover::None;
        if close.contains(pointer) { hover = Hover::Close; }
        else if expand.contains(pointer) { hover = Hover::Expand; }
        else if pin.contains(pointer) { hover = Hover::Pin; }
        else if full.contains(pointer) { hover = Hover::Card(0); }
        self.hover = hover;
        if hover == Hover::Card(0) {
            ctx.set_cursor_icon(egui::CursorIcon::Grab);
            if pressed { ctx.send_viewport_cmd(ViewportCommand::StartDrag); }
        } else {
            ctx.set_cursor_icon(egui::CursorIcon::PointingHand);
        }
        if clicked {
            match hover {
                Hover::Close => { ctx.send_viewport_cmd(ViewportCommand::Close); }
                Hover::Expand => self.exit_mini(ctx),
                Hover::Pin => self.toggle_top_most(ctx),
                _ => {}
            }
        }
        if let Some(r) = self.current_outer(ctx) {
            self.store.cfg.mini_x = r.min.x as i32;
            self.store.cfg.mini_y = r.min.y as i32;
            self.store.cfg.has_mini_pos = true;
        }
    }

    fn draw_sidebar(&self, ctx: &egui::Context, p: &egui::Painter, full: Rect) {
        let side = Rect::from_min_size(full.min, Vec2::new(ui::SIDEBAR, full.height()));
        ui::gradient(p, side, self.pal.sidebar_top, self.pal.sidebar_bottom);
        p.line_segment([side.right_top(), side.right_bottom()], Stroke::new(1.0, self.pal.overlay(10)));

        let logo = Rect::from_min_size(Pos2::new(side.left() + 26.0, side.top() + 24.0), Vec2::splat(40.0));
        let brand = self.pal.brand;
        ui::round_fill(p, logo, 12.0, brand);
        ui::round_fill(
            p,
            Rect::from_min_size(logo.min + Vec2::new(logo.width() * 0.18, logo.height() * 0.18), logo.size() * 0.64),
            10.0,
            ui::rgba(self.pal.brand2.r(), self.pal.brand2.g(), self.pal.brand2.b(), 110),
        );
        let clock = Rect::from_center_size(logo.center(), Vec2::splat(20.0));
        ui::icon_clock(p, clock, ui::rgba(255, 255, 255, 240), 1.7);
        p.circle_filled(clock.center(), 1.3, Color32::WHITE);

        let title = i18n::tr(self.en, "时光倒数", "SHIGUANG");
        p.text(
            Pos2::new(side.left() + 78.0, side.top() + 36.0),
            Align2::LEFT_CENTER,
            title,
            ui::ui_font(19.0),
            self.pal.ink,
        );
        p.text(
            Pos2::new(side.left() + 79.0, side.top() + 62.0),
            Align2::LEFT_CENTER,
            "C O U N T D O W N   C A L E N D A R",
            ui::ui_font(6.8),
            self.pal.text3,
        );

        let lr = self.language_rect(full);
        ui::round_fill(p, lr, 11.0, self.pal.overlay(if self.hover == Hover::Language { 12 } else { 5 }));
        ui::round_stroke(p, lr, 11.0, self.pal.overlay(12), 1.0);
        p.text(
            lr.center(),
            Align2::CENTER_CENTER,
            if self.en { "中" } else { "EN" },
            ui::ui_font(9.0),
            if self.hover == Hover::Language { self.pal.ink } else { self.pal.text2 },
        );

        p.text(
            Pos2::new(side.left() + 26.0, side.top() + 109.0),
            Align2::LEFT_CENTER,
            i18n::tr(self.en, "我的日子", "MY DAYS"),
            ui::ui_font(10.5),
            self.pal.text2,
        );
        p.text(
            Pos2::new(side.right() - 26.0, side.top() + 109.0),
            Align2::RIGHT_CENTER,
            i18n::event_count(self.store.cfg.events.len(), self.en),
            ui::ui_font(9.0),
            self.pal.text3,
        );
        p.line_segment(
            [Pos2::new(side.left() + 26.0, side.top() + 126.0), Pos2::new(side.right() - 26.0, side.top() + 126.0)],
            Stroke::new(1.0, self.pal.overlay(7)),
        );

        let list = self.list_rect(full);
        let lp = p.with_clip_rect(list);
        for i in 0..self.store.cfg.events.len() {
            let r = self.card_rect(full, i);
            if r.bottom() < list.top() || r.top() > list.bottom() {
                continue;
            }
            self.draw_event_card(ctx, &lp, r, i);
        }

        let max = self.scroll_max(full);
        if max > 0.0 {
            let track = Rect::from_min_size(
                Pos2::new(list.right() + 1.0, list.top() + 4.0),
                Vec2::new(3.0, list.height() - 8.0),
            );
            ui::round_fill(p, track, 1.5, self.pal.overlay(7));
            let thumb_h = (26.0f32).max(track.height() * list.height() / (list.height() + max));
            let thumb_y = track.top() + (track.height() - thumb_h) * (self.scroll / max);
            ui::round_fill(
                p,
                Rect::from_min_size(Pos2::new(track.left(), thumb_y), Vec2::new(3.0, thumb_h)),
                1.5,
                self.pal.overlay(28),
            );
        }

        self.draw_auto_row(p, full);

        let newr = self.new_rect(full);
        let accent = ui::mix(self.pal.brand, self.pal.brand2, 0.32);
        ui::round_fill(p, newr, 15.0, accent);
        ui::icon_plus(
            p,
            Rect::from_min_size(Pos2::new(newr.left() + 22.0, newr.top() + 17.0), Vec2::splat(16.0)),
            ui::c(13, 19, 33),
            1.9,
        );
        p.text(
            Pos2::new(newr.left() + 48.0, newr.center().y),
            Align2::LEFT_CENTER,
            i18n::tr(self.en, "新建日子", "New day"),
            ui::ui_font(13.0),
            ui::c(13, 19, 33),
        );

        let status = if self.store.dirty {
            i18n::tr(self.en, "已自动保存 · 退出后写回 EXE", "Saved · writes to EXE on exit")
        } else {
            i18n::tr(self.en, "配置已内嵌 · 自动保存", "Stored inside EXE · autosaved")
        };
        let dot_color = if self.store.dirty { self.pal.warn } else { self.pal.success };
        let sf = ui::ui_font(8.5);
        let sw = ui::measure(ctx, &status, &sf).x;
        let sx = side.center().x - (sw + 12.0) / 2.0;
        p.circle_filled(Pos2::new(sx, side.bottom() - 18.0), 2.5, dot_color);
        p.text(
            Pos2::new(sx + 12.0, side.bottom() - 18.0),
            Align2::LEFT_CENTER,
            status,
            sf,
            self.pal.text3,
        );
    }

    fn draw_auto_row(&self, p: &egui::Painter, full: Rect) {
        let r = self.auto_row_rect(full);
        if self.hover == Hover::AutoStart {
            ui::round_fill(p, r, 9.0, self.pal.overlay(6));
        }
        p.text(
            Pos2::new(r.left() + 8.0, r.center().y),
            Align2::LEFT_CENTER,
            i18n::tr(self.en, "开机启动", "Start with Windows"),
            ui::ui_font(9.0),
            if self.hover == Hover::AutoStart { self.pal.ink } else { self.pal.text3 },
        );
        let sw = Rect::from_min_size(Pos2::new(r.right() - 42.0, r.top() + 4.0), Vec2::new(34.0, 18.0));
        ui::draw_switch(p, sw, self.auto_start, ui::accent_ink(self.pal.brand, self.pal.light), &self.pal);
    }

    fn draw_event_card(&self, ctx: &egui::Context, p: &egui::Painter, r: Rect, i: usize) {
        let ev = &self.store.cfg.events[i];
        let sel = self.selected == Some(i);
        let hov = self.hover == Hover::Card(i);
        let accent = ui::from_hex(&ev.accent);
        let accent_ink = ui::accent_ink(accent, self.pal.light);
        if sel {
            ui::round_fill(p, r, 14.0, ui::rgba(accent.r(), accent.g(), accent.b(), if self.pal.light { 22 } else { 27 }));
            ui::round_stroke(
                p,
                r.shrink(0.5),
                14.0,
                ui::rgba(accent.r(), accent.g(), accent.b(), 95),
                1.0,
            );
            ui::round_fill(
                p,
                Rect::from_min_size(Pos2::new(r.left() + 9.0, r.top() + 20.0), Vec2::new(3.0, 36.0)),
                1.5,
                accent,
            );
        } else {
            if hov {
                ui::round_fill(p, r, 14.0, self.pal.overlay(10));
                ui::round_stroke(p, r.shrink(0.5), 14.0, self.pal.overlay(16), 1.0);
            }
            ui::round_fill(
                p,
                Rect::from_min_size(Pos2::new(r.left() + 9.0, r.top() + 20.0), Vec2::new(3.0, 36.0)),
                1.5,
                ui::rgba(accent.r(), accent.g(), accent.b(), if hov { 150 } else { 90 }),
            );
        }

        let now = config::now();
        let target = ev.effective_target(now);
        let future = target > now;
        let diff = target.signed_duration_since(now);
        let total = if future { diff.num_seconds() } else { (-diff).num_seconds() };
        let days = total / 86400;
        let title_font = ui::ui_font(12.5);
        let title = ui::ellipsize(ctx, &ev.title, r.width() - 96.0, &title_font);
        p.text(
            Pos2::new(r.left() + 20.0, r.top() + 24.0),
            Align2::LEFT_CENTER,
            title,
            title_font,
            self.pal.ink,
        );
        let date_text = format!(
            "{}{}",
            i18n::short_date(target, self.en),
            if ev.repeat_yearly { i18n::tr(self.en, " · 每年", " · yearly") } else { String::new() }
        );
        p.text(
            Pos2::new(r.left() + 20.0, r.top() + 49.0),
            Align2::LEFT_CENTER,
            ui::ellipsize(ctx, &date_text, r.width() - 100.0, &ui::ui_font(9.0)),
            ui::ui_font(9.0),
            if sel { self.pal.text2 } else { self.pal.text3 },
        );

        let word = if target.date() == now.date() {
            Some(i18n::today(self.en))
        } else if future && target.date() == now.date() + Duration::days(1) {
            Some(i18n::tomorrow(self.en))
        } else if !future && target.date() == now.date() - Duration::days(1) {
            Some(i18n::yesterday(self.en))
        } else {
            None
        };
        if let Some(word) = word {
            p.text(
                Pos2::new(r.right() - 18.0, r.top() + 27.0),
                Align2::RIGHT_CENTER,
                word,
                ui::ui_font(11.5),
                if sel { accent_ink } else { self.pal.text2 },
            );
        } else {
            let ds = days.to_string();
            let mut size = 23.0;
            let mut f = ui::display_font(size);
            while size > 13.0 && ui::measure(ctx, &ds, &f).x > 62.0 {
                size -= 1.5;
                f = ui::display_font(size);
            }
            p.text(
                Pos2::new(r.right() - 18.0, r.top() + 27.0),
                Align2::RIGHT_CENTER,
                ds,
                f,
                if sel { accent_ink } else { self.pal.ink },
            );
            p.text(
                Pos2::new(r.right() - 18.0, r.top() + 52.0),
                Align2::RIGHT_CENTER,
                i18n::days_unit(future, self.en),
                ui::ui_font(8.5),
                self.pal.text3,
            );
        }
    }

    fn draw_main_panel(&self, ctx: &egui::Context, p: &egui::Painter, full: Rect) {
        self.draw_window_buttons(p, full);
        if self.selected.is_none() {
            self.draw_empty(ctx, p, full);
            self.draw_footer(ctx, p, full);
            return;
        }
        let ev = self.selected_event().unwrap();
        let original = ev.target_dt();
        let now = config::now();
        let target = ev.effective_target(now);
        let accent = ui::from_hex(&ev.accent);
        let accent_ink = ui::accent_ink(accent, self.pal.light);
        let editr = self.edit_rect(full);
        let title_font = ui::ui_font(23.0);
        p.text(
            Pos2::new(full.left() + ui::SIDEBAR + 42.0, full.top() + 88.0),
            Align2::LEFT_CENTER,
            ui::ellipsize(ctx, &ev.title, editr.left() - (full.left() + ui::SIDEBAR + 42.0) - 18.0, &title_font),
            title_font,
            self.pal.ink,
        );
        p.text(
            Pos2::new(full.left() + ui::SIDEBAR + 42.0, full.top() + 121.0),
            Align2::LEFT_CENTER,
            i18n::date(target, self.en),
            ui::ui_font(10.5),
            self.pal.text2,
        );

        let mode_label = i18n::mode_name(ev.mode, self.en);
        let mf = ui::ui_font(8.5);
        let mw = ui::measure(ctx, &mode_label, &mf).x + 26.0;
        let mx = full.left() + ui::SIDEBAR + 42.0 + ui::measure(ctx, &i18n::date(target, self.en), &ui::ui_font(10.5)).x + 14.0;
        let chip = Rect::from_min_size(Pos2::new(mx, full.top() + 110.0), Vec2::new(mw, 20.0));
        ui::round_fill(p, chip, 10.0, self.pal.overlay(6));
        ui::round_stroke(p, chip, 10.0, self.pal.overlay(11), 1.0);
        p.circle_filled(Pos2::new(chip.left() + 11.0, chip.center().y), 1.5, accent);
        p.text(
            chip.center(),
            Align2::CENTER_CENTER,
            mode_label,
            mf.clone(),
            self.pal.text2,
        );
        if ev.repeat_yearly {
            let yearly_text = i18n::tr(self.en, "每年", "yearly");
            let rw = ui::measure(ctx, &yearly_text, &mf).x + 18.0;
            let rc = Rect::from_min_size(Pos2::new(chip.right() + 8.0, full.top() + 110.0), Vec2::new(rw, 20.0));
            ui::round_fill(p, rc, 10.0, ui::rgba(accent.r(), accent.g(), accent.b(), if self.pal.light { 30 } else { 34 }));
            ui::round_stroke(p, rc, 10.0, ui::rgba(accent.r(), accent.g(), accent.b(), 90), 1.0);
            p.text(
                rc.center(),
                Align2::CENTER_CENTER,
                i18n::tr(self.en, "每年", "yearly"),
                mf.clone(),
                accent_ink,
            );
        }

        ui::round_fill(p, editr, 17.0, self.pal.overlay(if self.hover == Hover::Edit { 13 } else { 7 }));
        ui::round_stroke(p, editr, 17.0, self.pal.overlay(if self.hover == Hover::Edit { 24 } else { 16 }), 1.0);
        ui::icon_pencil(
            p,
            Rect::from_min_size(Pos2::new(editr.left() + 15.0, editr.top() + 11.0), Vec2::splat(13.0)),
            if self.hover == Hover::Edit { self.pal.ink } else { self.pal.text2 },
            1.2,
        );
        p.text(
            Pos2::new(editr.left() + 33.0, editr.center().y),
            Align2::LEFT_CENTER,
            i18n::tr(self.en, "编辑", "Edit"),
            ui::ui_font(10.5),
            if self.hover == Hover::Edit { self.pal.ink } else { self.pal.text2 },
        );

        self.draw_hero(ctx, p, full, ev, original, target, accent, accent_ink);
        self.draw_footer(ctx, p, full);
    }

    fn draw_window_buttons(&self, p: &egui::Painter, full: Rect) {
        if self.mini {
            let close = self.mini_button(full, 0);
            let expand = self.mini_button(full, 1);
            let pin = self.mini_button(full, 2);
            self.draw_win_button(p, close, Hover::Close, false, |p, r, c| ui::icon_close(p, r, c, 1.6));
            self.draw_win_button(p, expand, Hover::Expand, false, |p, r, c| ui::icon_expand(p, r, c, 1.5));
            self.draw_win_button(p, pin, Hover::Pin, self.top_most, |p, r, c| ui::icon_pin(p, r, c, 1.5));
            return;
        }
        let theme = self.top_button(full, 4);
        let mini = self.top_button(full, 3);
        let pin = self.top_button(full, 2);
        let min = self.top_button(full, 1);
        let close = self.top_button(full, 0);
        self.draw_win_button(p, theme, Hover::Theme, false, |p, r, c| match self.theme_mode {
            2 => ui::icon_moon(p, r, c, 1.5),
            1 => ui::icon_sun(p, r, c, 1.3),
            _ => ui::icon_monitor(p, r, c, 1.4),
        });
        self.draw_win_button(p, mini, Hover::Mini, false, |p, r, c| ui::icon_mini(p, r, c, 1.5));
        self.draw_win_button(p, pin, Hover::Pin, self.top_most, |p, r, c| ui::icon_pin(p, r, c, 1.5));
        self.draw_win_button(p, min, Hover::Min, false, |p, r, c| ui::icon_minus(p, r, c, 1.5));
        self.draw_win_button(p, close, Hover::Close, false, |p, r, c| ui::icon_close(p, r, c, 1.6));
    }

    fn draw_win_button<F>(&self, p: &egui::Painter, r: Rect, hover: Hover, active: bool, icon: F)
    where
        F: FnOnce(&egui::Painter, Rect, Color32),
    {
        let hov = self.hover == hover;
        if hov && hover == Hover::Close {
            ui::round_fill(p, r, 8.0, ui::rgba(self.pal.danger.r(), self.pal.danger.g(), self.pal.danger.b(), 38));
        } else if active {
            ui::round_fill(p, r, 8.0, ui::rgba(self.pal.brand.r(), self.pal.brand.g(), self.pal.brand.b(), 36));
        } else if hov {
            ui::round_fill(p, r, 8.0, self.pal.overlay(10));
        }
        let color = if hover == Hover::Close {
            if hov { self.pal.danger } else { self.pal.text2 }
        } else if active {
            ui::accent_ink(self.pal.brand, self.pal.light)
        } else if hov {
            self.pal.ink
        } else {
            self.pal.text2
        };
        icon(p, r.shrink(9.0), color);
    }

    fn draw_hero(
        &self,
        ctx: &egui::Context,
        p: &egui::Painter,
        full: Rect,
        ev: &Event,
        original: NaiveDateTime,
        target: NaiveDateTime,
        accent: Color32,
        accent_ink: Color32,
    ) {
        let hero = self.hero_rect(full);
        ui::shadow(p, hero.translate(Vec2::new(0.0, 3.0)), 24.0, 1.0, if self.pal.light { 18 } else { 60 });
        ui::round_fill(p, hero, 24.0, self.pal.card_top);
        ui::round_stroke(p, hero.shrink(0.5), 24.0, self.pal.overlay(14), 1.0);

        let now = config::now();
        let future = target > now;
        let repeat_annual = ev.repeat_yearly && ev.mode != 2;
        let cycle = repeat_annual && original <= now;

        let mut chip = if repeat_annual {
            format!(
                "{} · {}",
                if self.en {
                    format!("Until {}", i18n::date(target, true))
                } else {
                    format!("距离 {} 还有", i18n::date(target, false))
                },
                i18n::anniversary(ev.anniversary_number(target), self.en)
            )
        } else if future {
            if self.en { format!("Until {}", i18n::date(target, true)) } else { format!("距离 {} 还有", i18n::date(target, false)) }
        } else if ev.mode == 1 {
            if self.en { format!("Reached {}", i18n::date(target, true)) } else { format!("目标 {} 已到达", i18n::date(target, false)) }
        } else if self.en {
            format!("Since {}", i18n::date(target, true))
        } else {
            format!("自 {} 起 已经过去", i18n::date(target, false))
        };
        if !repeat_annual && ev.repeat_yearly {
            chip.push_str(if self.en { " · yearly" } else { " · 每年" });
        }
        let cf = ui::ui_font(10.0);
        let chip_text = ui::ellipsize(ctx, &chip, hero.width() - 100.0, &cf);
        let cw = (ui::measure(ctx, &chip_text, &cf).x + 34.0).min(hero.width() - 80.0);
        let cr = Rect::from_min_size(Pos2::new(hero.center().x - cw / 2.0, hero.top() + 28.0), Vec2::new(cw, 30.0));
        ui::round_fill(p, cr, 15.0, self.pal.overlay(6));
        ui::round_stroke(p, cr, 15.0, self.pal.overlay(11), 1.0);
        p.circle_filled(Pos2::new(cr.left() + 13.0, cr.center().y), 2.5, accent);
        p.text(cr.center(), Align2::CENTER_CENTER, chip_text, cf, self.pal.text2);

        let (_, days, hours, minutes, seconds, _) = config::split_duration(target, now);
        let day_text = days.to_string();
        let mut size = 100.0;
        let mut df = ui::display_font(size);
        while size > 50.0 && ui::measure(ctx, &day_text, &df).x > hero.width() * 0.52 {
            size -= 5.0;
            df = ui::display_font(size);
        }
        let nw = ui::measure(ctx, &day_text, &df).x;
        let uf = ui::display_font(18.0);
        let unit = i18n::day_word(days, self.en);
        let uw = ui::measure(ctx, &unit, &uf).x;
        let avail_top = hero.top() + 74.0;
        let avail_bottom = hero.bottom() - 135.0;
        let block_h = 256.0;
        let block_top = avail_top + ((avail_bottom - avail_top - block_h).max(0.0)) / 2.0;
        let group_w = nw + uw + 8.0;
        let gx = hero.center().x - group_w / 2.0;
        let line_h = ctx.fonts(|f| f.row_height(&df));
        let num_rect = Rect::from_min_size(Pos2::new(gx, block_top), Vec2::new(nw + 2.0, line_h + 4.0));
        if !self.pal.light {
            p.text(
                Pos2::new(num_rect.center().x, num_rect.center().y + 4.0),
                Align2::CENTER_CENTER,
                &day_text,
                df.clone(),
                ui::rgba(0, 0, 0, 85),
            );
        }
        let shade = if self.pal.light { accent_ink } else { ui::mix(accent, Color32::WHITE, 0.42) };
        p.text(
            Pos2::new(num_rect.center().x, num_rect.center().y),
            Align2::CENTER_CENTER,
            &day_text,
            df,
            shade,
        );
        p.text(
            Pos2::new(gx + nw + 8.0, num_rect.bottom() - 6.0),
            Align2::LEFT_BOTTOM,
            unit,
            uf,
            self.pal.text2,
        );

        let pill_w = 136.0;
        let pill_h = 76.0;
        let gap = 14.0;
        let total = pill_w * 3.0 + gap * 2.0;
        let px = hero.center().x - total / 2.0;
        let py = block_top + 180.0;
        let vals = [hours, minutes, seconds];
        let labels = [
            i18n::tr(self.en, "时", "HRS"),
            i18n::tr(self.en, "分", "MIN"),
            i18n::tr(self.en, "秒", "SEC"),
        ];
        for i in 0..3 {
            let pr = Rect::from_min_size(Pos2::new(px + i as f32 * (pill_w + gap), py), Vec2::new(pill_w, pill_h));
            ui::round_fill(p, pr, 18.0, self.pal.overlay(7));
            ui::round_stroke(p, pr, 18.0, self.pal.overlay(12), 1.0);
            p.text(
                Pos2::new(pr.center().x, pr.top() + 28.0),
                Align2::CENTER_CENTER,
                format!("{:02}", vals[i]),
                ui::display_font(27.0),
                self.pal.ink,
            );
            p.text(
                Pos2::new(pr.center().x, pr.top() + 56.0),
                Align2::CENTER_CENTER,
                &labels[i],
                ui::ui_font(9.0),
                self.pal.text2,
            );
        }

        let (mut start, caption, left, right) = if cycle {
            let start = ev.progress_start(now);
            (
                start,
                i18n::tr(self.en, "年度进度", "Year progress"),
                i18n::tr(self.en, "上次 · ", "Last · ") + &i18n::short_day(start, self.en),
                i18n::tr(self.en, "下次 · ", "Next · ") + &i18n::short_day(target, self.en),
            )
        } else {
            let start = ev.created_dt();
            (
                start,
                i18n::tr(self.en, "时间进度", "Time progress"),
                i18n::tr(self.en, "创建 · ", "Created · ") + &i18n::short_day(start, self.en),
                (if repeat_annual { i18n::tr(self.en, "下次 · ", "Next · ") } else { i18n::tr(self.en, "目标 · ", "Target · ") })
                    + &i18n::short_day(target, self.en),
            )
        };
        let total = target.signed_duration_since(start).num_seconds().max(1) as f64;
        let done = now.signed_duration_since(start).num_seconds().max(0) as f64;
        let frac = (done / total).clamp(0.0, 1.0);
        let lx = hero.left() + 46.0;
        let rx = hero.right() - 46.0;
        let label_y = hero.bottom() - 115.0;
        p.text(
            Pos2::new(lx, label_y),
            Align2::LEFT_CENTER,
            caption,
            ui::ui_font(9.0),
            self.pal.text3,
        );
        p.text(
            Pos2::new(rx, label_y),
            Align2::RIGHT_CENTER,
            format!("{}%", (frac * 100.0).round() as i32),
            ui::ui_font(10.0),
            accent_ink,
        );
        let track_y = hero.bottom() - 91.0;
        let track = Rect::from_min_size(Pos2::new(lx, track_y), Vec2::new(rx - lx, 6.0));
        ui::round_fill(p, track, 3.0, self.pal.overlay(12));
        let fill = ((rx - lx) as f64 * frac) as f32;
        let fillr = Rect::from_min_size(Pos2::new(lx, track_y), Vec2::new(fill.max(6.0), 6.0));
        ui::round_fill(p, fillr, 3.0, accent);
        let kx = lx + fill.max(6.0);
        p.circle_filled(Pos2::new(kx, track_y + 3.0), 5.0, accent);
        p.circle_filled(Pos2::new(kx, track_y + 3.0), 2.0, ui::rgba(255, 255, 255, 220));
        p.text(
            Pos2::new(lx, track_y + 23.0),
            Align2::LEFT_CENTER,
            left,
            ui::ui_font(8.5),
            self.pal.text3,
        );
        p.text(
            Pos2::new(rx, track_y + 23.0),
            Align2::RIGHT_CENTER,
            right,
            ui::ui_font(8.5),
            self.pal.text3,
        );
    }

    fn draw_footer(&self, ctx: &egui::Context, p: &egui::Painter, full: Rect) {
        let status = if self.store.dirty {
            i18n::tr(self.en, "已自动保存 · 退出时写回 EXE", "Saved · writes to EXE on exit")
        } else {
            i18n::tr(self.en, "配置已内嵌 EXE · 自动保存", "Stored inside EXE · autosaved")
        };
        let sf = ui::ui_font(8.5);
        let x = full.left() + ui::SIDEBAR + 42.0;
        p.circle_filled(Pos2::new(x, full.bottom() - 39.0), 2.5, if self.store.dirty { self.pal.warn } else { self.pal.success });
        p.text(
            Pos2::new(x + 12.0, full.bottom() - 39.0),
            Align2::LEFT_CENTER,
            status,
            sf.clone(),
            self.pal.text3,
        );
        p.text(
            Pos2::new(full.right() - 42.0, full.bottom() - 39.0),
            Align2::RIGHT_CENTER,
            if self.en { "v0.2 · Shiguang" } else { "v0.2 · 时光倒数" },
            sf,
            self.pal.text3,
        );
    }

    fn draw_empty(&self, ctx: &egui::Context, p: &egui::Painter, full: Rect) {
        let cx = full.left() + ui::SIDEBAR + (full.width() - ui::SIDEBAR) / 2.0;
        let cy = full.top() + full.height() * 0.38;

        let icon = Rect::from_center_size(Pos2::new(cx, cy - 20.0), Vec2::splat(76.0));
        ui::round_stroke(p, icon.expand(18.0), 22.0, self.pal.overlay(9), 1.0);
        ui::icon_calendar(p, icon, ui::accent_ink(self.pal.brand, self.pal.light), 2.2);
        p.text(
            Pos2::new(cx, cy + 80.0),
            Align2::CENTER_CENTER,
            i18n::tr(self.en, "把重要的日子，留在这里", "Keep the days that matter"),
            ui::ui_font(22.0),
            self.pal.ink,
        );
        p.text(
            Pos2::new(cx, cy + 116.0),
            Align2::CENTER_CENTER,
            i18n::tr(self.en, "设置一个日期与时间，剩下的交给时间。", "Pick a date and time; let time do the rest."),
            ui::ui_font(10.5),
            self.pal.text2,
        );
        let cta = Rect::from_center_size(
            Pos2::new(cx, full.bottom() - 250.0 + 23.0),
            Vec2::new(192.0, 46.0),
        );
        ui::round_fill(p, cta, 15.0, ui::mix(self.pal.brand, self.pal.brand2, 0.32));
        ui::icon_plus(
            p,
            Rect::from_min_size(Pos2::new(cta.left() + 26.0, cta.top() + 15.0), Vec2::splat(16.0)),
            ui::c(13, 19, 33),
            1.9,
        );
        p.text(
            Pos2::new(cta.left() + 52.0, cta.center().y),
            Align2::LEFT_CENTER,
            i18n::tr(self.en, "新建第一个日子", "Create your first day"),
            ui::ui_font(12.0),
            ui::c(13, 19, 33),
        );
        let _ = ctx;
    }
}

fn resolve_light(ctx: &egui::Context, mode: i32) -> bool {
    match mode {
        1 => true,
        2 => false,
        _ => ctx
            .system_theme()
            .map(|t| t == egui::Theme::Light)
            .unwrap_or(false),
    }
}

impl eframe::App for App {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        ctx.set_theme(egui::ThemePreference::from(if self.pal.light {
            egui::Theme::Light
        } else {
            egui::Theme::Dark
        }));

        if self.theme_mode == 0 {
            let light = resolve_light(ctx, 0);
            if light != self.pal.light {
                self.pal = Pal::new(light);
            }
        }

        let now = Instant::now();
        if self.last_outer.is_some()
            && now.duration_since(self.last_bounds_save) > StdDuration::from_millis(900)
        {
            let before = (
                self.store.cfg.win_x,
                self.store.cfg.win_y,
                self.store.cfg.win_w,
                self.store.cfg.win_h,
                self.store.cfg.mini,
                self.store.cfg.mini_x,
                self.store.cfg.mini_y,
            );
            self.capture_bounds(ctx);
            let after = (
                self.store.cfg.win_x,
                self.store.cfg.win_y,
                self.store.cfg.win_w,
                self.store.cfg.win_h,
                self.store.cfg.mini,
                self.store.cfg.mini_x,
                self.store.cfg.mini_y,
            );
            if before != after {
                self.save();
                self.last_bounds_save = now;
            }
        }
        self.last_outer = self.current_outer(ctx);

        egui::CentralPanel::default()
            .frame(egui::Frame::NONE.fill(Color32::TRANSPARENT))
            .show(ctx, |ui| {
                let full = ui.max_rect();
                if let Some(pos) = ui.ctx().input(|i| i.pointer.hover_pos()) {
                    self.mouse = pos;
                }
                if let Some(mut ed) = self.editor.take() {
                    self.draw_editor(ui, full, &mut ed);
                    if self.title_focus {
                        self.title_focus = false;
                    }
                    if ed.save_requested {
                        self.commit_editor(&ed);
                    } else if ed.delete_requested {
                        self.delete_selected();
                    } else if !ed.close_requested {
                        self.editor = Some(ed);
                    } else if ed.close_requested {
                        // cancelled
                    }
                } else {
                    self.draw_main(ui, full);
                }
            });

        ctx.request_repaint_after(StdDuration::from_millis(500));
        if self.theme_mode == 0 && now.duration_since(self.last_theme_check) > StdDuration::from_secs(2) {
            self.last_theme_check = now;
        }
    }
}

impl Drop for App {
    fn drop(&mut self) {
        // The eframe context is gone by now; bounds were captured in the last frame.
        self.store.save();
        self.store.finish_exit();
    }
}
include!("app_editor.rs");
