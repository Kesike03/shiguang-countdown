#![windows_subsystem = "windows"]

mod app;
mod config;
mod i18n;
mod ui;

use eframe::egui;
use std::path::Path;
use std::sync::Arc;

fn make_icon() -> egui::IconData {
    let s = 64usize;
    let mut rgba = vec![0u8; s * s * 4];
    let c1 = (124.0f32, 156.0f32, 255.0f32);
    let c2 = (177.0f32, 140.0f32, 255.0f32);
    for y in 0..s {
        for x in 0..s {
            let px = x as f32 + 0.5;
            let py = y as f32 + 0.5;
            let half = 30.0f32;
            let rad = 14.0f32;
            let qx = (px - 32.0).abs() - (half - rad);
            let qy = (py - 32.0).abs() - (half - rad);
            let ox = qx.max(0.0);
            let oy = qy.max(0.0);
            let dist = (ox * ox + oy * oy).sqrt() + qx.max(qy).min(0.0) - rad;
            let bg_cov = (0.8 - dist).clamp(0.0, 1.0);
            let t = (px + py) / 128.0;
            let mut r = c1.0 + (c2.0 - c1.0) * t;
            let mut g = c1.1 + (c2.1 - c1.1) * t;
            let mut b = c1.2 + (c2.2 - c1.2) * t;
            let mut a = 255.0 * bg_cov;

            // clock ring
            let dx = px - 32.0;
            let dy = py - 32.0;
            let rr = (dx * dx + dy * dy).sqrt();
            if rr > 12.0 && rr < 18.0 {
                let cov = (1.2 - (rr - 15.0).abs()).clamp(0.0, 1.0);
                if cov > 0.0 {
                    r = r + (255.0 - r) * cov;
                    g = g + (255.0 - g) * cov;
                    b = b + (255.0 - b) * cov;
                    a = a.max(255.0 * cov);
                }
            }
            // hands
            for (x2, y2, w) in [(32.0, 22.0, 2.0f32), (40.0, 37.0, 2.0f32)] {
                let vx = x2 - 32.0;
                let vy = y2 - 32.0;
                let wx = px - 32.0;
                let wy = py - 32.0;
                let len2 = vx * vx + vy * vy;
                let dot = (wx * vx + wy * vy) / len2;
                let t2 = dot.clamp(0.0, 1.0);
                let hx = wx - vx * t2;
                let hy = wy - vy * t2;
                let hd = (hx * hx + hy * hy).sqrt();
                let cov = (w - hd).clamp(0.0, 1.0);
                if cov > 0.0 {
                    r = r + (255.0 - r) * cov;
                    g = g + (255.0 - g) * cov;
                    b = b + (255.0 - b) * cov;
                    a = a.max(255.0 * cov);
                }
            }
            let center_cov = (2.2 - rr).clamp(0.0, 1.0);
            if center_cov > 0.0 {
                r = r + (255.0 - r) * center_cov;
                g = g + (255.0 - g) * center_cov;
                b = b + (255.0 - b) * center_cov;
                a = a.max(255.0 * center_cov);
            }
            let i = (y * s + x) * 4;
            rgba[i] = r as u8;
            rgba[i + 1] = g as u8;
            rgba[i + 2] = b as u8;
            rgba[i + 3] = a as u8;
        }
    }
    egui::IconData {
        rgba,
        width: s as u32,
        height: s as u32,
    }
}

fn main() -> eframe::Result<()> {
    let args: Vec<String> = std::env::args().collect();
    if args.len() >= 4 && args[1] == "--patch" {
        config::run_patcher(Path::new(&args[2]), Path::new(&args[3]));
        return Ok(());
    }

    let store = config::Store::open();
    let cfg = store.cfg.clone();
    let (w, h) = if cfg.mini {
        (ui::MINI_W, ui::MINI_H)
    } else if cfg.has_win_size {
        (cfg.win_w.max(ui::MIN_W as i32) as f32, cfg.win_h.max(ui::MIN_H as i32) as f32)
    } else {
        (1100.0, 760.0)
    };
    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_inner_size([w, h])
            .with_min_inner_size([if cfg.mini { ui::MINI_W } else { ui::MIN_W }, if cfg.mini { ui::MINI_H } else { ui::MIN_H }])
            .with_decorations(false)
            .with_resizable(!cfg.mini)
            .with_icon(Arc::new(make_icon()))
            .with_title("Shiguang Countdown"),
        ..Default::default()
    };
    eframe::run_native(
        "Shiguang Countdown",
        options,
        Box::new(move |cc| {
            ui::setup_fonts(&cc.egui_ctx);
            Ok(Box::new(app::App::new(cc, store)))
        }),
    )
}
