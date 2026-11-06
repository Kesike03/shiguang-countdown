impl App {
    fn draw_mini(&self, ctx: &egui::Context, p: &egui::Painter, full: Rect) {
        ui::gradient(p, full, self.pal.bg_top, self.pal.bg_bottom);
        ui::round_stroke(p, full.shrink(0.5), 20.0, self.pal.overlay(14), 1.0);
        self.draw_window_buttons(p, full);
        if self.selected.is_none() {
            p.text(
                Pos2::new(full.left() + 24.0, full.top() + 35.0),
                Align2::LEFT_CENTER,
                i18n::tr(self.en, "还没有日子", "No days yet"),
                ui::ui_font(15.0),
                self.pal.ink,
            );
            p.text(
                Pos2::new(full.left() + 24.0, full.top() + 64.0),
                Align2::LEFT_CENTER,
                i18n::tr(self.en, "展开窗口，创建第一个日子", "Expand to create your first day"),
                ui::ui_font(9.5),
                self.pal.text2,
            );
            return;
        }
        let ev = self.selected_event().unwrap();
        let now = config::now();
        let target = ev.effective_target(now);
        let span = target.signed_duration_since(now);
        let future = span.num_seconds() > 0;
        let total = if future { span.num_seconds() } else { (-span).num_seconds() };
        let days = total / 86400;
        let rem = total % 86400;
        let accent = ui::from_hex(&ev.accent);
        let accent_ink = ui::accent_ink(accent, self.pal.light);

        let tf = ui::ui_font(14.0);
        p.text(
            Pos2::new(full.left() + 24.0, full.top() + 30.0),
            Align2::LEFT_CENTER,
            ui::ellipsize(ctx, &ev.title, full.width() - 140.0, &tf),
            tf,
            self.pal.ink,
        );
        let sub = format!(
            "{}{}{}",
            i18n::short_date(target, self.en),
            if future { i18n::tr(self.en, " · 还有", " · left") } else { i18n::tr(self.en, " · 已过去", " · ago") },
            if ev.repeat_yearly { i18n::tr(self.en, " · 每年", " · yearly") } else { String::new() }
        );
        p.text(
            Pos2::new(full.left() + 24.0, full.top() + 52.0),
            Align2::LEFT_CENTER,
            sub,
            ui::ui_font(9.0),
            self.pal.text2,
        );

        let day_text = days.to_string();
        let mut size = 60.0;
        let mut df = ui::display_font(size);
        while size > 40.0 && ui::measure(ctx, &day_text, &df).x > full.width() * 0.42 {
            size -= 4.0;
            df = ui::display_font(size);
        }
        let nw = ui::measure(ctx, &day_text, &df).x;
        let line_h = ctx.fonts(|f| f.row_height(&df));
        p.text(
            Pos2::new(full.left() + 24.0, full.top() + 80.0 + line_h / 2.0),
            Align2::LEFT_CENTER,
            &day_text,
            df,
            if self.pal.light { accent_ink } else { ui::mix(accent, Color32::WHITE, 0.42) },
        );
        p.text(
            Pos2::new(full.left() + 24.0 + nw + 8.0, full.top() + 80.0 + line_h - 8.0),
            Align2::LEFT_BOTTOM,
            i18n::day_word(days, self.en),
            ui::ui_font(13.0),
            self.pal.text2,
        );
        p.text(
            Pos2::new(full.left() + 24.0, full.top() + 171.0),
            Align2::LEFT_CENTER,
            format!("{:02}:{:02}:{:02}", rem / 3600, (rem % 3600) / 60, rem % 60),
            ui::display_font(23.0),
            self.pal.ink,
        );
        p.text(
            Pos2::new(full.right() - 24.0, full.top() + 172.0),
            Align2::RIGHT_CENTER,
            if ev.repeat_yearly { i18n::tr(self.en, "每年重复", "Yearly repeat") } else { i18n::tr(self.en, "仅此一次", "One-off") },
            ui::ui_font(8.5),
            self.pal.text2,
        );
        let track = Rect::from_min_size(
            Pos2::new(full.left() + 24.0, full.bottom() - 18.0),
            Vec2::new(full.width() - 48.0, 4.0),
        );
        ui::round_fill(p, track, 2.0, self.pal.overlay(12));
        let start = if ev.repeat_yearly && ev.mode != 2 { ev.progress_start(now) } else { ev.created_dt() };
        let total_secs = target.signed_duration_since(start).num_seconds().max(1) as f64;
        let done = now.signed_duration_since(start).num_seconds().max(0) as f64;
        let frac = (done / total_secs).clamp(0.0, 1.0) as f32;
        let fw = (track.width() * frac).max(4.0);
        ui::round_fill(
            p,
            Rect::from_min_size(track.min, Vec2::new(fw, 4.0)),
            2.0,
            accent,
        );
    }

    fn editor_card(full: Rect) -> Rect {
        let w = 560.0;
        let h = 720.0;
        Rect::from_min_size(
            Pos2::new(full.center().x - w / 2.0, full.center().y - h / 2.0),
            Vec2::new(w, h),
        )
    }

    fn draw_editor(&mut self, ui: &mut egui::Ui, full: Rect, ed: &mut Editor) {
        let ctx = ui.ctx().clone();
        let p = ui.painter().clone();
        let card = Self::editor_card(full);
        p.rect_filled(full, 0, ui::rgba(3, 7, 14, if self.pal.light { 90 } else { 135 }));
        ui::shadow(&p, card.translate(Vec2::new(0.0, 4.0)), 18.0, 1.0, if self.pal.light { 40 } else { 100 });
        ui::round_fill(&p, card, 16.0, self.pal.dialog_top);
        ui::round_stroke(&p, card.shrink(0.5), 16.0, self.pal.overlay(18), 1.0);

        let pointer = ctx.input(|i| i.pointer.hover_pos()).unwrap_or(self.mouse);
        let primary = ctx.input(|i| i.pointer.primary_clicked());
        let mut hover_hand = false;

        let title_rect = Rect::from_min_size(Pos2::new(card.left() + 30.0, card.top() + 88.0), Vec2::new(500.0, 46.0));
        let close = Rect::from_min_size(Pos2::new(card.right() - 58.0, card.top() + 18.0), Vec2::new(28.0, 28.0));
        let panel = Rect::from_min_size(Pos2::new(card.left() + 30.0, card.top() + 176.0), Vec2::new(500.0, 270.0));
        let year_prev = Rect::from_min_size(Pos2::new(card.left() + 44.0, card.top() + 186.0), Vec2::new(26.0, 28.0));
        let year_next = Rect::from_min_size(Pos2::new(card.left() + 130.0, card.top() + 186.0), Vec2::new(26.0, 28.0));
        let month_prev = Rect::from_min_size(Pos2::new(card.left() + 178.0, card.top() + 186.0), Vec2::new(26.0, 28.0));
        let month_next = Rect::from_min_size(Pos2::new(card.left() + 256.0, card.top() + 186.0), Vec2::new(26.0, 28.0));
        let today_btn = Rect::from_min_size(Pos2::new(card.left() + 394.0, card.top() + 186.0), Vec2::new(54.0, 28.0));
        let save_btn = Rect::from_min_size(Pos2::new(card.left() + 410.0, card.top() + 654.0), Vec2::new(120.0, 42.0));
        let cancel_btn = Rect::from_min_size(Pos2::new(card.left() + 302.0, card.top() + 654.0), Vec2::new(96.0, 42.0));
        let delete_btn = Rect::from_min_size(Pos2::new(card.left() + 30.0, card.top() + 654.0), Vec2::new(118.0, 42.0));
        let repeat_row = Rect::from_min_size(Pos2::new(card.left() + 356.0, card.top() + 452.0), Vec2::new(174.0, 26.0));
        let repeat_sw = Rect::from_min_size(Pos2::new(card.left() + 496.0, card.top() + 456.0), Vec2::new(34.0, 18.0));
        let seg = Rect::from_min_size(Pos2::new(card.left() + 30.0, card.top() + 566.0), Vec2::new(300.0, 38.0));
        let now_btn = Rect::from_min_size(Pos2::new(card.left() + 410.0, card.top() + 482.0), Vec2::new(120.0, 44.0));

        if ctx.input(|i| i.key_pressed(Key::Escape)) {
            ed.close_requested = true;
        }

        let in_title = Rect::from_min_size(Pos2::new(card.left() + 44.0, card.top() + 99.0), Vec2::new(472.0, 26.0)).contains(pointer);

        if primary && !in_title {
            if close.contains(pointer) {
                ed.close_requested = true;
            } else if year_prev.contains(pointer) {
                if ed.view_year > 1900 { ed.view_year -= 1; }
            } else if year_next.contains(pointer) {
                if ed.view_year < 2200 { ed.view_year += 1; }
            } else if month_prev.contains(pointer) {
                if ed.view_month == 1 { ed.view_month = 12; ed.view_year -= 1; } else { ed.view_month -= 1; }
            } else if month_next.contains(pointer) {
                if ed.view_month == 12 { ed.view_month = 1; ed.view_year += 1; } else { ed.view_month += 1; }
            } else if today_btn.contains(pointer) {
                let t = Local::now().date_naive();
                ed.date = t;
                ed.view_year = t.year();
                ed.view_month = t.month();
            } else if now_btn.contains(pointer) {
                let t = Local::now().naive_local();
                ed.date = t.date();
                ed.hour = t.hour();
                ed.minute = t.minute();
                ed.second = t.second();
                ed.view_year = t.year();
                ed.view_month = t.month();
            } else if repeat_row.contains(pointer) {
                ed.repeat = !ed.repeat;
            } else if save_btn.contains(pointer) {
                ed.save_requested = true;
            } else if cancel_btn.contains(pointer) {
                ed.close_requested = true;
            } else if !ed.is_new && delete_btn.contains(pointer) {
                ed.confirm_delete = true;
            } else if seg.contains(pointer) {
                ed.mode = (((pointer.x - seg.left()) / 100.0).floor() as i32).clamp(0, 2);
            } else {
                let mut time_hit = false;
                for i in 0..3 {
                    let fx = card.left() + [30.0, 140.0, 250.0][i];
                    let fr = Rect::from_min_size(Pos2::new(fx, card.top() + 482.0), Vec2::new(100.0, 44.0));
                    if fr.contains(pointer) {
                        let arrow = Rect::from_min_size(Pos2::new(fx + 70.0, card.top() + 485.0), Vec2::new(26.0, 36.0));
                        if arrow.contains(pointer) {
                            let delta = if pointer.y < card.top() + 503.0 { 1 } else { -1 };
                            Self::adjust_time(ed, i, delta);
                        }
                        time_hit = true;
                    }
                }
                if !time_hit {
                    let mut swatch_hit = false;
                    for i in 0..ACCENTS.len() {
                        let col = i % 4;
                        let row = i / 4;
                        let r = Rect::from_min_size(
                            Pos2::new(card.left() + 356.0 + col as f32 * 40.0, card.top() + 566.0 + row as f32 * 34.0),
                            Vec2::splat(28.0),
                        );
                        if r.contains(pointer) {
                            ed.accent = ui::from_hex(ACCENTS[i]);
                            swatch_hit = true;
                        }
                    }
                    if !swatch_hit {
                        let grid_top = card.top() + 246.0;
                        let grid_left = card.left() + 30.0;
                        if pointer.x >= grid_left && pointer.x < grid_left + 500.0 && pointer.y >= grid_top && pointer.y < grid_top + 6.0 * 32.0 {
                            let col = ((pointer.x - grid_left) / (500.0 / 7.0)).floor() as i32;
                            let row = ((pointer.y - grid_top) / 32.0).floor() as i32;
                            let first = NaiveDate::from_ymd_opt(ed.view_year, ed.view_month, 1).unwrap();
                            let offset = first.weekday().num_days_from_monday() as i32;
                            let day = row * 7 + col - offset + 1;
                            let days_in = config::days_in_month(ed.view_year, ed.view_month) as i32;
                            if day >= 1 && day <= days_in && (0..7).contains(&col) {
                                ed.date = NaiveDate::from_ymd_opt(ed.view_year, ed.view_month, day as u32).unwrap();
                            }
                        }
                    }
                }
            }
        }

        if !in_title {
            let scroll = ctx.input(|i| i.raw_scroll_delta.y);
            if scroll.abs() > 0.1 {
                for i in 0..3 {
                    let fx = card.left() + [30.0, 140.0, 250.0][i];
                    let fr = Rect::from_min_size(Pos2::new(fx, card.top() + 482.0), Vec2::new(100.0, 44.0));
                    if fr.contains(pointer) {
                        Self::adjust_time(ed, i, if scroll > 0.0 { 1 } else { -1 });
                    }
                }
            }
        }

        if close.contains(pointer) || save_btn.contains(pointer) || cancel_btn.contains(pointer)
            || (!ed.is_new && delete_btn.contains(pointer)) || repeat_row.contains(pointer)
            || year_prev.contains(pointer) || year_next.contains(pointer) || month_prev.contains(pointer)
            || month_next.contains(pointer) || today_btn.contains(pointer) || now_btn.contains(pointer)
            || seg.contains(pointer)
            || (Rect::from_min_size(Pos2::new(card.left() + 356.0, card.top() + 566.0), Vec2::new(174.0, 64.0)).contains(pointer))
        {
            hover_hand = true;
        }
        if hover_hand {
            ctx.set_cursor_icon(egui::CursorIcon::PointingHand);
        }

        p.text(
            Pos2::new(card.left() + 30.0, card.top() + 31.0),
            Align2::LEFT_CENTER,
            if ed.is_new { i18n::tr(self.en, "新建日子", "New day") } else { i18n::tr(self.en, "编辑日子", "Edit day") },
            ui::ui_font(15.5),
            self.pal.ink,
        );
        if close.contains(pointer) {
            ui::round_fill(&p, close, 7.0, ui::rgba(self.pal.danger.r(), self.pal.danger.g(), self.pal.danger.b(), 28));
        }
        ui::icon_close(&p, close.shrink(7.0), if close.contains(pointer) { self.pal.danger } else { self.pal.text2 }, 1.5);

        p.text(
            Pos2::new(card.left() + 30.0, title_rect.top() - 11.0),
            Align2::LEFT_CENTER,
            i18n::tr(self.en, "名称", "Name"),
            ui::ui_font(9.5),
            self.pal.text2,
        );
        ui::round_fill(&p, title_rect, 13.0, self.pal.field);
        let title_focused = ctx.memory(|m| m.focused()) == Some(Id::new("editor_title"));
        ui::round_stroke(
            &p,
            title_rect,
            13.0,
            if title_focused { ed.accent } else { self.pal.overlay(11) },
            1.0,
        );

        ui::round_fill(&p, panel, 16.0, self.pal.overlay(4));
        ui::round_stroke(&p, panel, 16.0, self.pal.overlay(8), 1.0);
        ui::icon_chevron(&p, year_prev.shrink(6.0), self.pal.text2, 1.3, true);
        ui::icon_chevron(&p, year_next.shrink(6.0), self.pal.text2, 1.3, false);
        ui::icon_chevron(&p, month_prev.shrink(6.0), self.pal.text2, 1.3, true);
        ui::icon_chevron(&p, month_next.shrink(6.0), self.pal.text2, 1.3, false);
        let month_date = NaiveDate::from_ymd_opt(ed.view_year, ed.view_month, 1)
            .unwrap()
            .and_hms_opt(0, 0, 0)
            .unwrap();
        p.text(
            Pos2::new(card.left() + 72.0, card.top() + 200.0),
            Align2::LEFT_CENTER,
            i18n::year_label(ed.view_year, self.en),
            ui::ui_font(13.0),
            self.pal.ink,
        );
        p.text(
            Pos2::new(card.left() + 206.0, card.top() + 200.0),
            Align2::LEFT_CENTER,
            i18n::month_label(month_date, self.en),
            ui::ui_font(13.0),
            self.pal.ink,
        );
        if today_btn.contains(pointer) {
            ui::round_fill(&p, today_btn, 8.0, self.pal.overlay(8));
        }
        p.text(
            today_btn.center(),
            Align2::CENTER_CENTER,
            i18n::tr(self.en, "今天", "Today"),
            ui::ui_font(9.5),
            if today_btn.contains(pointer) { self.pal.ink } else { self.pal.text2 },
        );
        let wf = ui::ui_font(8.0);
        for c in 0..7 {
            let cell = Rect::from_min_size(
                Pos2::new(card.left() + 30.0 + c as f32 * (500.0 / 7.0), card.top() + 222.0),
                Vec2::new(500.0 / 7.0, 18.0),
            );
            p.text(cell.center(), Align2::CENTER_CENTER, i18n::week_header(c, self.en), wf.clone(), self.pal.text2);
        }
        let first = NaiveDate::from_ymd_opt(ed.view_year, ed.view_month, 1).unwrap();
        let offset = first.weekday().num_days_from_monday() as i32;
        let days_in = config::days_in_month(ed.view_year, ed.view_month);
        let today = Local::now().date_naive();
        let df = ui::ui_font(10.5);
        for d in 1..=days_in {
            let slot = offset + d as i32 - 1;
            let row = slot / 7;
            let col = slot % 7;
            let cell = Rect::from_min_size(
                Pos2::new(card.left() + 30.0 + col as f32 * (500.0 / 7.0), card.top() + 246.0 + row as f32 * 32.0),
                Vec2::new(500.0 / 7.0, 32.0),
            );
            let date = NaiveDate::from_ymd_opt(ed.view_year, ed.view_month, d).unwrap();
            let sel = date == ed.date;
            let is_today = date == today;
            if sel {
                p.circle_filled(cell.center(), 15.0, ed.accent);
            } else if is_today {
                p.circle_stroke(cell.center(), 15.0, Stroke::new(1.0, ui::rgba(ed.accent.r(), ed.accent.g(), ed.accent.b(), 140)));
            }
            p.text(
                cell.center(),
                Align2::CENTER_CENTER,
                d.to_string(),
                df.clone(),
                if sel { ui::c(15, 21, 35) } else if is_today { ui::accent_ink(ed.accent, self.pal.light) } else { self.pal.ink },
            );
        }

        p.text(
            Pos2::new(card.left() + 30.0, card.top() + 468.0),
            Align2::LEFT_CENTER,
            i18n::tr(self.en, "时间", "Time"),
            ui::ui_font(9.5),
            self.pal.text2,
        );
        p.text(
            Pos2::new(card.left() + 356.0, card.top() + 466.0),
            Align2::LEFT_CENTER,
            i18n::tr(self.en, "每年重复", "Repeat yearly"),
            ui::ui_font(9.5),
            self.pal.text2,
        );
        ui::draw_switch(&p, repeat_sw, ed.repeat, ed.accent, &self.pal);
        let time_font = ui::display_font(20.0);
        let unit_font = ui::ui_font(8.5);
        let vals = [ed.hour, ed.minute, ed.second];
        let units = [
            i18n::tr(self.en, "时", "h"),
            i18n::tr(self.en, "分", "m"),
            i18n::tr(self.en, "秒", "s"),
        ];
        for i in 0..3 {
            let fx = card.left() + [30.0, 140.0, 250.0][i];
            let fr = Rect::from_min_size(Pos2::new(fx, card.top() + 482.0), Vec2::new(100.0, 44.0));
            ui::round_fill(&p, fr, 13.0, self.pal.field);
            ui::round_stroke(&p, fr, 13.0, self.pal.overlay(11), 1.0);
            p.text(Pos2::new(fr.left() + 11.0, fr.center().y), Align2::LEFT_CENTER, format!("{:02}", vals[i]), time_font.clone(), self.pal.ink);
            p.text(Pos2::new(fr.left() + 52.0, fr.center().y), Align2::LEFT_CENTER, &units[i], unit_font.clone(), self.pal.text3);
            ui::icon_chevron_v(&p, Rect::from_min_size(Pos2::new(fr.right() - 29.0, fr.top() + 8.0), Vec2::new(12.0, 8.0)), self.pal.text3, 1.1, true);
            ui::icon_chevron_v(&p, Rect::from_min_size(Pos2::new(fr.right() - 29.0, fr.bottom() - 16.0), Vec2::new(12.0, 8.0)), self.pal.text3, 1.1, false);
        }
        p.text(Pos2::new(card.left() + 130.0, card.top() + 504.0), Align2::CENTER_CENTER, ":", ui::ui_font(13.0), self.pal.text3);
        p.text(Pos2::new(card.left() + 240.0, card.top() + 504.0), Align2::CENTER_CENTER, ":", ui::ui_font(13.0), self.pal.text3);
        ui::round_fill(&p, now_btn, 12.0, self.pal.overlay(if now_btn.contains(pointer) { 8 } else { 3 }));
        ui::round_stroke(&p, now_btn, 12.0, self.pal.overlay(10), 1.0);
        ui::icon_clock(&p, Rect::from_min_size(Pos2::new(now_btn.left() + 14.0, now_btn.top() + 14.0), Vec2::splat(16.0)), self.pal.text3, 1.3);
        p.text(
            Pos2::new(now_btn.left() + 38.0, now_btn.center().y),
            Align2::LEFT_CENTER,
            i18n::tr(self.en, "设为现在", "Set to now"),
            ui::ui_font(10.0),
            if now_btn.contains(pointer) { self.pal.ink } else { self.pal.text2 },
        );

        p.text(Pos2::new(card.left() + 30.0, card.top() + 552.0), Align2::LEFT_CENTER, i18n::tr(self.en, "模式", "Mode"), ui::ui_font(9.5), self.pal.text3);
        p.text(Pos2::new(card.left() + 356.0, card.top() + 552.0), Align2::LEFT_CENTER, i18n::tr(self.en, "颜色", "Color"), ui::ui_font(9.5), self.pal.text3);
        ui::round_fill(&p, seg, 19.0, self.pal.overlay(6));
        ui::round_stroke(&p, seg, 19.0, self.pal.overlay(9), 1.0);
        for i in 0..3 {
            let r = Rect::from_min_size(Pos2::new(seg.left() + 3.0 + i as f32 * 100.0, seg.top() + 3.0), Vec2::new(94.0, 32.0));
            if ed.mode == i as i32 {
                ui::round_fill(&p, r, 16.0, ed.accent);
            }
            p.text(
                r.center(),
                Align2::CENTER_CENTER,
                i18n::mode_name(i as i32, self.en),
                ui::ui_font(10.5),
                if ed.mode == i as i32 { ui::c(14, 20, 34) } else { self.pal.text2 },
            );
        }
        p.text(
            Pos2::new(card.left() + 30.0, card.top() + 618.0),
            Align2::LEFT_CENTER,
            i18n::mode_desc(ed.mode, self.en),
            ui::ui_font(9.0),
            self.pal.text3,
        );
        for i in 0..ACCENTS.len() {
            let col = i % 4;
            let row = i / 4;
            let sr = Rect::from_min_size(Pos2::new(card.left() + 356.0 + col as f32 * 40.0, card.top() + 566.0 + row as f32 * 34.0), Vec2::splat(28.0));
            let color = ui::from_hex(ACCENTS[i]);
            let sel = color == ed.accent;
            if sel {
                p.circle_stroke(sr.center(), 17.0, Stroke::new(1.2, self.pal.ink));
            }
            p.circle_filled(sr.center(), 13.0, color);
            if sel {
                p.circle_filled(sr.center(), 3.0, ui::c(17, 23, 38));
            }
        }

        if !ed.is_new {
            if delete_btn.contains(pointer) {
                ui::round_fill(&p, delete_btn, 12.0, ui::rgba(self.pal.danger.r(), self.pal.danger.g(), self.pal.danger.b(), 24));
            }
            ui::icon_trash(&p, Rect::from_min_size(Pos2::new(delete_btn.left() + 12.0, delete_btn.top() + 12.0), Vec2::splat(16.0)), self.pal.danger, 1.2);
            p.text(Pos2::new(delete_btn.left() + 36.0, delete_btn.center().y), Align2::LEFT_CENTER, i18n::tr(self.en, "删除", "Delete"), ui::ui_font(9.5), self.pal.danger);
        }
        ui::round_fill(&p, cancel_btn, 13.0, self.pal.overlay(if cancel_btn.contains(pointer) { 12 } else { 5 }));
        ui::round_stroke(&p, cancel_btn, 13.0, self.pal.overlay(if cancel_btn.contains(pointer) { 20 } else { 11 }), 1.0);
        p.text(cancel_btn.center(), Align2::CENTER_CENTER, i18n::tr(self.en, "取消", "Cancel"), ui::ui_font(10.5), if cancel_btn.contains(pointer) { self.pal.ink } else { self.pal.text2 });
        ui::round_fill(&p, save_btn, 13.0, ed.accent);
        p.text(save_btn.center(), Align2::CENTER_CENTER, i18n::tr(self.en, "保存", "Save"), ui::ui_font(11.0), ui::c(14, 20, 34));

        let edit_rect = Rect::from_min_size(Pos2::new(card.left() + 44.0, card.top() + 99.0), Vec2::new(472.0, 26.0));
        let te = egui::TextEdit::singleline(&mut ed.title)
            .id(Id::new("editor_title"))
            .frame(false)
            .hint_text(i18n::placeholder(self.en))
            .font(ui::ui_font(13.0))
            .text_color(self.pal.ink)
            .margin(egui::Margin::symmetric(0, 0));
        let resp = ui.put(edit_rect, te);
        if ed.just_opened {
            resp.request_focus();
            ed.just_opened = false;
        }

        if ed.confirm_delete {
            let overlay = Rect::from_center_size(card.center(), Vec2::new(360.0, 150.0));
            ui::shadow(&p, overlay, 16.0, 1.0, 80);
            ui::round_fill(&p, overlay, 16.0, self.pal.dialog_top);
            ui::round_stroke(&p, overlay, 16.0, self.pal.overlay(24), 1.0);
            p.text(
                Pos2::new(overlay.center().x, overlay.top() + 42.0),
                Align2::CENTER_CENTER,
                i18n::delete_confirm(&ed.title, self.en),
                ui::ui_font(10.5),
                self.pal.ink,
            );
            let yes = Rect::from_min_size(Pos2::new(overlay.left() + 38.0, overlay.bottom() - 52.0), Vec2::new(130.0, 34.0));
            let no = Rect::from_min_size(Pos2::new(overlay.right() - 168.0, overlay.bottom() - 52.0), Vec2::new(130.0, 34.0));
            ui::round_fill(&p, yes, 10.0, self.pal.danger);
            p.text(yes.center(), Align2::CENTER_CENTER, i18n::tr(self.en, "删除", "Delete"), ui::ui_font(10.5), Color32::WHITE);
            ui::round_fill(&p, no, 10.0, self.pal.overlay(8));
            p.text(no.center(), Align2::CENTER_CENTER, i18n::tr(self.en, "取消", "Cancel"), ui::ui_font(10.5), self.pal.text2);
            if primary {
                if yes.contains(pointer) {
                    ed.delete_requested = true;
                } else if no.contains(pointer) {
                    ed.confirm_delete = false;
                }
            }
        }
    }

    fn adjust_time(ed: &mut Editor, idx: usize, delta: i32) {
        match idx {
            0 => ed.hour = ((ed.hour as i32 + delta + 24) % 24) as u32,
            1 => ed.minute = ((ed.minute as i32 + delta + 60) % 60) as u32,
            _ => ed.second = ((ed.second as i32 + delta + 60) % 60) as u32,
        }
    }
}
