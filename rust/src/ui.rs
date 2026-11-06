use eframe::egui::{self, Color32, FontData, FontDefinitions, FontFamily, FontId, Painter, Pos2, Rect, Stroke, StrokeKind, Vec2};
use std::sync::Arc;

pub const SIDEBAR: f32 = 306.0;
pub const MIN_W: f32 = 1000.0;
pub const MIN_H: f32 = 760.0;
pub const MINI_W: f32 = 380.0;
pub const MINI_H: f32 = 230.0;

#[derive(Clone)]
pub struct Pal {
    pub light: bool,
    pub bg_top: Color32,
    pub bg_bottom: Color32,
    pub sidebar_top: Color32,
    pub sidebar_bottom: Color32,
    pub card_top: Color32,
    pub card_bottom: Color32,
    pub field: Color32,
    pub ink: Color32,
    pub text2: Color32,
    pub text3: Color32,
    pub danger: Color32,
    pub success: Color32,
    pub warn: Color32,
    pub brand: Color32,
    pub brand2: Color32,
    pub dialog_top: Color32,
    pub dialog_bottom: Color32,
    pub tooltip_bg: Color32,
    pub tooltip_text: Color32,
}

impl Pal {
    pub fn new(light: bool) -> Self {
        if light {
            Self {
                light,
                bg_top: c(249, 251, 255),
                bg_bottom: c(232, 238, 248),
                sidebar_top: c(255, 255, 255),
                sidebar_bottom: c(244, 247, 252),
                card_top: c(255, 255, 255),
                card_bottom: c(246, 249, 254),
                field: c(237, 242, 250),
                ink: c(24, 34, 53),
                text2: c(86, 102, 127),
                text3: c(139, 153, 173),
                danger: c(214, 69, 69),
                success: c(19, 138, 99),
                warn: c(200, 121, 10),
                brand: c(124, 156, 255),
                brand2: c(177, 140, 255),
                dialog_top: c(255, 255, 255),
                dialog_bottom: c(243, 247, 253),
                tooltip_bg: c(255, 255, 255),
                tooltip_text: c(55, 70, 92),
            }
        } else {
            Self {
                light,
                bg_top: c(12, 18, 33),
                bg_bottom: c(7, 11, 21),
                sidebar_top: c(13, 19, 33),
                sidebar_bottom: c(8, 13, 24),
                card_top: c(24, 35, 57),
                card_bottom: c(15, 23, 39),
                field: c(24, 34, 54),
                ink: c(238, 243, 251),
                text2: c(160, 172, 194),
                text3: c(118, 130, 154),
                danger: c(255, 118, 118),
                success: c(110, 231, 183),
                warn: c(255, 196, 107),
                brand: c(124, 156, 255),
                brand2: c(177, 140, 255),
                dialog_top: c(20, 29, 47),
                dialog_bottom: c(10, 15, 26),
                tooltip_bg: c(31, 42, 65),
                tooltip_text: c(170, 181, 203),
            }
        }
    }

    pub fn overlay(&self, a: u8) -> Color32 {
        if self.light {
            rgba(23, 35, 55, (a as f32 * 0.78) as u8)
        } else {
            rgba(255, 255, 255, a)
        }
    }
}

pub fn c(r: u8, g: u8, b: u8) -> Color32 {
    Color32::from_rgb(r, g, b)
}

pub fn rgba(r: u8, g: u8, b: u8, a: u8) -> Color32 {
    Color32::from_rgba_unmultiplied(r, g, b, a)
}

pub fn from_hex(s: &str) -> Color32 {
    let h = s.trim_start_matches('#');
    if h.len() == 6 {
        if let Ok(v) = u32::from_str_radix(h, 16) {
            return c(((v >> 16) & 255) as u8, ((v >> 8) & 255) as u8, (v & 255) as u8);
        }
    }
    c(124, 156, 255)
}

pub fn mix(a: Color32, b: Color32, t: f32) -> Color32 {
    let t = t.clamp(0.0, 1.0);
    c(
        (a.r() as f32 + (b.r() as f32 - a.r() as f32) * t) as u8,
        (a.g() as f32 + (b.g() as f32 - a.g() as f32) * t) as u8,
        (a.b() as f32 + (b.b() as f32 - a.b() as f32) * t) as u8,
    )
}

fn rgb_to_hsl(color: Color32) -> (f32, f32, f32) {
    let r = color.r() as f32 / 255.0;
    let g = color.g() as f32 / 255.0;
    let b = color.b() as f32 / 255.0;
    let max = r.max(g).max(b);
    let min = r.min(g).min(b);
    let l = (max + min) / 2.0;
    let d = max - min;
    let s = if d < 1e-6 {
        0.0
    } else if l > 0.5 {
        d / (2.0 - max - min)
    } else {
        d / (max + min)
    };
    let h = if d < 1e-6 {
        0.0
    } else if max == r {
        ((g - b) / d + if g < b { 6.0 } else { 0.0 }) / 6.0
    } else if max == g {
        ((b - r) / d + 2.0) / 6.0
    } else {
        ((r - g) / d + 4.0) / 6.0
    };
    (h, s, l)
}

fn hue(p: f32, q: f32, t: f32) -> f32 {
    let mut t = t;
    if t < 0.0 {
        t += 1.0;
    }
    if t > 1.0 {
        t -= 1.0;
    }
    if t < 1.0 / 6.0 {
        p + (q - p) * 6.0 * t
    } else if t < 0.5 {
        q
    } else if t < 2.0 / 3.0 {
        p + (q - p) * (2.0 / 3.0 - t) * 6.0
    } else {
        p
    }
}

fn hsl_to_rgb(h: f32, s: f32, l: f32) -> Color32 {
    if s <= 0.0 {
        let v = (l * 255.0) as u8;
        return c(v, v, v);
    }
    let q = if l < 0.5 { l * (1.0 + s) } else { l + s - l * s };
    let p = 2.0 * l - q;
    c(
        (hue(p, q, h + 1.0 / 3.0).clamp(0.0, 1.0) * 255.0) as u8,
        (hue(p, q, h).clamp(0.0, 1.0) * 255.0) as u8,
        (hue(p, q, h - 1.0 / 3.0).clamp(0.0, 1.0) * 255.0) as u8,
    )
}

pub fn accent_ink(color: Color32, light: bool) -> Color32 {
    if !light {
        return mix(color, Color32::WHITE, 0.15);
    }
    let (h, s, l) = rgb_to_hsl(color);
    hsl_to_rgb(h, s.max(0.62), l.min(0.44))
}

pub fn ui_font(size: f32) -> FontId {
    FontId::new(size, FontFamily::Proportional)
}

pub fn display_font(size: f32) -> FontId {
    FontId::new(size, FontFamily::Name(Arc::from("display")))
}

pub fn setup_fonts(ctx: &egui::Context) {
    let mut fonts = FontDefinitions::default();
    let mut cn_loaded = false;
    for path in [
        "C:\\Windows\\Fonts\\msyh.ttc",
        "C:\\Windows\\Fonts\\Deng.ttf",
        "C:\\Windows\\Fonts\\simhei.ttf",
    ] {
        if let Ok(bytes) = std::fs::read(path) {
            fonts
                .font_data
                .insert("cn".to_owned(), Arc::new(FontData::from_owned(bytes)));
            cn_loaded = true;
            break;
        }
    }
    if cn_loaded {
        if let Some(fam) = fonts.families.get_mut(&FontFamily::Proportional) {
            fam.insert(0, "cn".to_owned());
        }
    }

    let mut display_loaded = false;
    if let Ok(bytes) = std::fs::read("C:\\Windows\\Fonts\\bahnschrift.ttf") {
        fonts
            .font_data
            .insert("display".to_owned(), Arc::new(FontData::from_owned(bytes)));
        display_loaded = true;
    }
    let mut display_family = Vec::new();
    if display_loaded {
        display_family.push("display".to_owned());
    }
    if cn_loaded {
        display_family.push("cn".to_owned());
    }
    if display_family.is_empty() {
        display_family.push("Proportional".to_owned());
    }
    fonts
        .families
        .insert(FontFamily::Name(Arc::from("display")), display_family);
    ctx.set_fonts(fonts);
}

pub fn round_fill(p: &Painter, rect: Rect, radius: f32, fill: Color32) {
    p.rect_filled(rect, (radius.max(0.0) as u8).min(255), fill);
}

pub fn round_stroke(p: &Painter, rect: Rect, radius: f32, color: Color32, width: f32) {
    p.rect_stroke(
        rect,
        (radius.max(0.0) as u8).min(255),
        Stroke::new(width, color),
        StrokeKind::Inside,
    );
}

pub fn text(
    p: &Painter,
    pos: Pos2,
    align: egui::Align2,
    s: impl ToString,
    font: FontId,
    color: Color32,
) {
    p.text(pos, align, s.to_string(), font, color);
}

pub fn measure(ctx: &egui::Context, s: &str, font: &FontId) -> Vec2 {
    ctx.fonts(|f| f.layout_no_wrap(s.to_owned(), font.clone(), Color32::WHITE).size())
}

pub fn ellipsize(ctx: &egui::Context, s: &str, maxw: f32, font: &FontId) -> String {
    if measure(ctx, s, font).x <= maxw {
        return s.to_string();
    }
    let mut out = String::new();
    for ch in s.chars() {
        let mut trial = out.clone();
        trial.push(ch);
        trial.push('…');
        if measure(ctx, &trial, font).x > maxw {
            break;
        }
        out.push(ch);
    }
    out.push('…');
    out
}

pub fn gradient(p: &Painter, rect: Rect, top: Color32, bottom: Color32) {
    let mut mesh = egui::Mesh::default();
    let base = mesh.vertices.len() as u32;
    mesh.vertices.push(egui::epaint::Vertex { pos: rect.left_top(), uv: Pos2::ZERO, color: top });
    mesh.vertices.push(egui::epaint::Vertex { pos: rect.right_top(), uv: Pos2::ZERO, color: top });
    mesh.vertices.push(egui::epaint::Vertex { pos: rect.right_bottom(), uv: Pos2::ZERO, color: bottom });
    mesh.vertices.push(egui::epaint::Vertex { pos: rect.left_bottom(), uv: Pos2::ZERO, color: bottom });
    mesh.indices.extend_from_slice(&[base, base + 1, base + 2, base, base + 2, base + 3]);
    p.add(egui::Shape::mesh(mesh));
}

pub fn shadow(p: &Painter, rect: Rect, radius: f32, spread: f32, alpha: u8) {
    let mut r = rect;
    r = Rect::from_min_max(r.min + Vec2::splat(spread), r.max + Vec2::splat(spread));
    round_fill(p, r, radius + spread, rgba(0, 0, 0, alpha));
}

pub fn draw_switch(p: &Painter, rect: Rect, on: bool, accent: Color32, pal: &Pal) {
    let radius = rect.height() / 2.0;
    round_fill(p, rect, radius, if on { accent } else { pal.overlay(28) });
    if !on {
        round_stroke(p, rect, radius, pal.overlay(38), 1.0);
    }
    let knob = rect.height() - 4.0;
    let kx = if on { rect.right() - knob - 2.0 } else { rect.left() + 2.0 };
    let kr = Rect::from_min_size(Pos2::new(kx, rect.top() + 2.0), Vec2::splat(knob));
    p.circle_filled(kr.center(), knob / 2.0, Color32::WHITE);
}

// ---------------- icons ----------------
pub fn icon_plus(p: &Painter, r: Rect, color: Color32, w: f32) {
    let ctr = r.center();
    let k = r.width().min(r.height()) * 0.28;
    p.line_segment([Pos2::new(ctr.x - k, ctr.y), Pos2::new(ctr.x + k, ctr.y)], Stroke::new(w, color));
    p.line_segment([Pos2::new(ctr.x, ctr.y - k), Pos2::new(ctr.x, ctr.y + k)], Stroke::new(w, color));
}

pub fn icon_close(p: &Painter, r: Rect, color: Color32, w: f32) {
    let ctr = r.center();
    let k = r.width().min(r.height()) * 0.25;
    p.line_segment([ctr + Vec2::splat(-k), ctr + Vec2::splat(k)], Stroke::new(w, color));
    p.line_segment([ctr + Vec2::new(k, -k), ctr + Vec2::new(-k, k)], Stroke::new(w, color));
}


pub fn icon_chevron_v(p: &Painter, r: Rect, color: Color32, w: f32, up: bool) {
    let ctr = r.center();
    let kx = r.width().min(r.height()) * 0.32;
    let ky = r.width().min(r.height()) * 0.22;
    if up {
        p.line_segment([Pos2::new(ctr.x - kx, ctr.y + ky), Pos2::new(ctr.x, ctr.y - ky)], Stroke::new(w, color));
        p.line_segment([Pos2::new(ctr.x, ctr.y - ky), Pos2::new(ctr.x + kx, ctr.y + ky)], Stroke::new(w, color));
    } else {
        p.line_segment([Pos2::new(ctr.x - kx, ctr.y - ky), Pos2::new(ctr.x, ctr.y + ky)], Stroke::new(w, color));
        p.line_segment([Pos2::new(ctr.x, ctr.y + ky), Pos2::new(ctr.x + kx, ctr.y - ky)], Stroke::new(w, color));
    }
}
pub fn icon_minus(p: &Painter, r: Rect, color: Color32, w: f32) {
    let ctr = r.center();
    let k = r.width().min(r.height()) * 0.28;
    p.line_segment([Pos2::new(ctr.x - k, ctr.y), Pos2::new(ctr.x + k, ctr.y)], Stroke::new(w, color));
}

pub fn icon_chevron(p: &Painter, r: Rect, color: Color32, w: f32, left: bool) {
    let ctr = r.center();
    let kx = r.width().min(r.height()) * 0.22;
    let ky = r.width().min(r.height()) * 0.32;
    if left {
        p.line_segment([Pos2::new(ctr.x + kx * 0.6, ctr.y - ky), Pos2::new(ctr.x - kx, ctr.y)], Stroke::new(w, color));
        p.line_segment([Pos2::new(ctr.x - kx, ctr.y), Pos2::new(ctr.x + kx * 0.6, ctr.y + ky)], Stroke::new(w, color));
    } else {
        p.line_segment([Pos2::new(ctr.x - kx * 0.6, ctr.y - ky), Pos2::new(ctr.x + kx, ctr.y)], Stroke::new(w, color));
        p.line_segment([Pos2::new(ctr.x + kx, ctr.y), Pos2::new(ctr.x - kx * 0.6, ctr.y + ky)], Stroke::new(w, color));
    }
}

pub fn icon_pencil(p: &Painter, r: Rect, color: Color32, w: f32) {
    let x0 = r.left() + r.width() * 0.18;
    let y0 = r.bottom() - r.height() * 0.18;
    let x1 = r.right() - r.width() * 0.20;
    let y1 = r.top() + r.height() * 0.20;
    p.line_segment([Pos2::new(x0, y0), Pos2::new(x1, y1)], Stroke::new(w, color));
    p.line_segment([Pos2::new(x0 - r.width() * 0.04, y0 + r.height() * 0.12), Pos2::new(x0 + r.width() * 0.12, y0 + r.height() * 0.28)], Stroke::new(w, color));
    p.line_segment([Pos2::new(x1 - r.width() * 0.02, y1 - r.height() * 0.10), Pos2::new(x1 + r.width() * 0.14, y1 + r.height() * 0.06)], Stroke::new(w, color));
}

pub fn icon_pin(p: &Painter, r: Rect, color: Color32, w: f32) {
    let cx = r.center().x;
    let top = r.top() + r.height() * 0.14;
    p.line_segment([Pos2::new(cx - r.width() * 0.26, top + r.height() * 0.02), Pos2::new(cx + r.width() * 0.26, top + r.height() * 0.02)], Stroke::new(w, color));
    p.line_segment([Pos2::new(cx - r.width() * 0.15, top + r.height() * 0.12), Pos2::new(cx + r.width() * 0.15, top + r.height() * 0.12)], Stroke::new(w, color));
    p.line_segment([Pos2::new(cx - r.width() * 0.15, top + r.height() * 0.12), Pos2::new(cx, top + r.height() * 0.52)], Stroke::new(w, color));
    p.line_segment([Pos2::new(cx + r.width() * 0.15, top + r.height() * 0.12), Pos2::new(cx, top + r.height() * 0.52)], Stroke::new(w, color));
    p.line_segment([Pos2::new(cx, top + r.height() * 0.52), Pos2::new(cx, r.bottom() - r.height() * 0.08)], Stroke::new(w, color));
}

pub fn icon_sun(p: &Painter, r: Rect, color: Color32, w: f32) {
    let ctr = r.center();
    let rad = r.width().min(r.height()) * 0.23;
    p.circle_stroke(ctr, rad, Stroke::new(w, color));
    for i in 0..8 {
        let a = std::f32::consts::PI / 4.0 * i as f32;
        let d1 = Vec2::new(a.cos(), a.sin()) * rad * 1.55;
        let d2 = Vec2::new(a.cos(), a.sin()) * rad * 2.05;
        p.line_segment([ctr + d1, ctr + d2], Stroke::new(w, color));
    }
}

pub fn icon_moon(p: &Painter, r: Rect, color: Color32, w: f32) {
    let ctr = r.center();
    let rad = r.width().min(r.height()) * 0.34;
    let mut last: Option<Pos2> = None;
    for i in 0..=24 {
        let a = 70.0f32.to_radians() + (230.0f32.to_radians() * i as f32 / 24.0);
        let pt = ctr + Vec2::new(a.cos(), a.sin()) * rad;
        if let Some(prev) = last {
            p.line_segment([prev, pt], Stroke::new(w, color));
        }
        last = Some(pt);
    }
}

pub fn icon_monitor(p: &Painter, r: Rect, color: Color32, w: f32) {
    let rr = Rect::from_min_size(
        Pos2::new(r.left() + r.width() * 0.08, r.top() + r.height() * 0.12),
        Vec2::new(r.width() * 0.84, r.height() * 0.60),
    );
    round_stroke(p, rr, r.width() * 0.08, color, w);
    p.line_segment([Pos2::new(r.left() + r.width() * 0.30, r.bottom() - r.height() * 0.08), Pos2::new(r.right() - r.width() * 0.30, r.bottom() - r.height() * 0.08)], Stroke::new(w, color));
    p.line_segment([Pos2::new(r.left() + r.width() * 0.42, r.top() + r.height() * 0.72), Pos2::new(r.left() + r.width() * 0.42, r.bottom() - r.height() * 0.08)], Stroke::new(w, color));
    p.line_segment([Pos2::new(r.right() - r.width() * 0.42, r.top() + r.height() * 0.72), Pos2::new(r.right() - r.width() * 0.42, r.bottom() - r.height() * 0.08)], Stroke::new(w, color));
}

pub fn icon_mini(p: &Painter, r: Rect, color: Color32, w: f32) {
    let rr = Rect::from_min_size(
        Pos2::new(r.left() + r.width() * 0.10, r.top() + r.height() * 0.14),
        Vec2::new(r.width() * 0.80, r.height() * 0.66),
    );
    round_stroke(p, rr, r.width() * 0.12, color, w);
    let inner = Rect::from_min_size(
        Pos2::new(r.left() + r.width() * 0.48, r.top() + r.height() * 0.46),
        Vec2::new(r.width() * 0.40, r.height() * 0.34),
    );
    p.rect_filled(inner, (r.width() * 0.07) as u8, color);
}

pub fn icon_expand(p: &Painter, r: Rect, color: Color32, w: f32) {
    let x1 = r.left() + r.width() * 0.16;
    let y1 = r.bottom() - r.height() * 0.16;
    let x2 = r.right() - r.width() * 0.16;
    let y2 = r.top() + r.height() * 0.16;
    p.line_segment([Pos2::new(x1, y1), Pos2::new(x2, y2)], Stroke::new(w, color));
    p.line_segment([Pos2::new(x2 - r.width() * 0.30, y2), Pos2::new(x2, y2)], Stroke::new(w, color));
    p.line_segment([Pos2::new(x2, y2 + r.height() * 0.30), Pos2::new(x2, y2)], Stroke::new(w, color));
    p.line_segment([Pos2::new(x1, y1), Pos2::new(x1, y1 - r.height() * 0.30)], Stroke::new(w * 0.85, color));
    p.line_segment([Pos2::new(x1, y1), Pos2::new(x1 + r.width() * 0.30, y1)], Stroke::new(w * 0.85, color));
}

pub fn icon_trash(p: &Painter, r: Rect, color: Color32, w: f32) {
    p.line_segment([Pos2::new(r.left() + r.width() * 0.18, r.top() + r.height() * 0.26), Pos2::new(r.right() - r.width() * 0.18, r.top() + r.height() * 0.26)], Stroke::new(w, color));
    p.line_segment([Pos2::new(r.left() + r.width() * 0.34, r.top() + r.height() * 0.12), Pos2::new(r.right() - r.width() * 0.34, r.top() + r.height() * 0.12)], Stroke::new(w, color));
    p.line_segment([Pos2::new(r.left() + r.width() * 0.26, r.top() + r.height() * 0.34), Pos2::new(r.left() + r.width() * 0.32, r.bottom() - r.height() * 0.08)], Stroke::new(w, color));
    p.line_segment([Pos2::new(r.right() - r.width() * 0.26, r.top() + r.height() * 0.34), Pos2::new(r.right() - r.width() * 0.32, r.bottom() - r.height() * 0.08)], Stroke::new(w, color));
}

pub fn icon_clock(p: &Painter, r: Rect, color: Color32, w: f32) {
    p.circle_stroke(r.center(), r.width().min(r.height()) / 2.0, Stroke::new(w, color));
    let ctr = r.center();
    p.line_segment([ctr, ctr + Vec2::new(0.0, -r.height() * 0.28)], Stroke::new(w, color));
    p.line_segment([ctr, ctr + Vec2::new(r.width() * 0.22, r.height() * 0.10)], Stroke::new(w, color));
}

pub fn icon_calendar(p: &Painter, r: Rect, color: Color32, w: f32) {
    let rr = Rect::from_min_size(
        Pos2::new(r.left(), r.top() + r.height() * 0.10),
        Vec2::new(r.width(), r.height() * 0.82),
    );
    round_stroke(p, rr, r.width() * 0.16, color, w);
    let y = r.top() + r.height() * 0.38;
    p.line_segment([Pos2::new(r.left() + r.width() * 0.08, y), Pos2::new(r.right() - r.width() * 0.08, y)], Stroke::new(w, color));
    p.line_segment([Pos2::new(r.left() + r.width() * 0.28, r.top() + r.height() * 0.02), Pos2::new(r.left() + r.width() * 0.28, r.top() + r.height() * 0.20)], Stroke::new(w, color));
    p.line_segment([Pos2::new(r.right() - r.width() * 0.28, r.top() + r.height() * 0.02), Pos2::new(r.right() - r.width() * 0.28, r.top() + r.height() * 0.20)], Stroke::new(w, color));
}
