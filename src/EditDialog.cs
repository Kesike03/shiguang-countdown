using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Shiguang
{
    internal sealed class EditDialog : Form
    {
        readonly EventItem _item;
        readonly bool _isNew;
        readonly TextBox _titleBox;


        public EventItem ResultItem;
        public bool Deleted;

        DateTime _selDate;
        DateTime _viewMonth;
        int _hour, _minute, _second;
        int _mode;
        bool _repeat;
        Color _accent;
        bool _titleIsHint;

        string _hot = "";
        DateTime? _hoverDate;
        Point _mouse;


        const int BaseW = 560;
        const int BaseH = 720;


        static readonly float[] TimeX = { 30f, 140f, 250f };

        public EditDialog(EventItem item, bool isNew)
        {
            _item = item;
            _isNew = isNew;
            Text = isNew ? L10n.T("新建日子", "New day") : L10n.T("编辑日子", "Edit day");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            KeyPreview = true;
            BackColor = Ui.BgBottom;
            ClientSize = new Size(Ui.S(BaseW), Ui.S(BaseH));
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            var target = EventUtil.TargetOf(item);
            _selDate = target.Date;
            _hour = target.Hour;
            _minute = target.Minute;
            _second = target.Second;
            _viewMonth = new DateTime(_selDate.Year, _selDate.Month, 1);
            _mode = Math.Max(0, Math.Min(2, item.Mode));
            _repeat = item.RepeatYearly;
            _accent = Ui.HexColor(item.Accent, Ui.Brand);

            _titleBox = new TextBox();
            _titleBox.BorderStyle = BorderStyle.None;
            _titleBox.BackColor = Ui.Field;
            _titleBox.ForeColor = Ui.Ink;
            _titleBox.Font = Ui.F(14);
            _titleBox.MaxLength = 48;
            _titleBox.ShortcutsEnabled = true;
            _titleBox.Bounds = new Rectangle(Ui.S(44), Ui.S(99), Ui.S(472), Ui.S(26));
            _titleBox.GotFocus += (s, e) => Invalidate();
            _titleBox.LostFocus += (s, e) => { if (!_titleIsHint && _titleBox.Text.Length == 0) ShowHint(); Invalidate(); };
            _titleBox.TextChanged += (s, e) =>
            {
                if (_titleIsHint && _titleBox.Text != Placeholder)
                {
                    _titleIsHint = false;
                    _titleBox.ForeColor = Ui.Ink;
                }
            };
            Controls.Add(_titleBox);

            if (string.IsNullOrEmpty(item.Title))
            {
                ShowHint();
            }
            else
            {
                _titleBox.Text = item.Title;
                _titleBox.ForeColor = Ui.Ink;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Ui.RoundWindow(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (Owner != null)
            {
                var wa = Screen.FromControl(Owner).WorkingArea;
                int nx = Math.Max(wa.Left + 8, Math.Min(Left, wa.Right - Width - 8));
                int ny = Math.Max(wa.Top + 8, Math.Min(Top, wa.Bottom - Height - 8));
                if (nx != Left || ny != Top) Location = new Point(nx, ny);
            }
            _titleBox.Top = Ui.S(88) + (Ui.S(46) - _titleBox.Height) / 2;
            if (_titleIsHint)
            {
                // Do not leave the grey example text selected in the real field.
                _titleBox.Text = "";
                _titleIsHint = false;
                _titleBox.ForeColor = Ui.Ink;
            }
            _titleBox.Focus();
            if (!string.IsNullOrEmpty(_titleBox.Text)) _titleBox.SelectAll();
        }

        string Placeholder { get { return L10n.T("例如：妈妈的生日", "e.g. A special day"); } }

        void ShowHint()
        {
            _titleIsHint = true;
            _titleBox.ForeColor = Ui.Text3;
            _titleBox.Text = Placeholder;
        }

        string GetTitle()
        {
            if (_titleIsHint) return "";
            return _titleBox.Text.Trim();
        }

        // ---------- geometry ----------
        RectangleF PanelRect { get { return Ui.RF(30, 176, 500, 270); } }
        RectangleF BtnCloseRect { get { return Ui.RF(502, 18, 28, 28); } }
        RectangleF BtnYearPrevRect { get { return Ui.RF(44, 186, 26, 28); } }
        RectangleF BtnYearNextRect { get { return Ui.RF(130, 186, 26, 28); } }
        RectangleF BtnMonthPrevRect { get { return Ui.RF(178, 186, 26, 28); } }
        RectangleF BtnMonthNextRect { get { return Ui.RF(256, 186, 26, 28); } }
        RectangleF BtnTodayRect { get { return Ui.RF(394, 186, 54, 28); } }
        RectangleF BtnNowRect { get { return Ui.RF(410, 482, 120, 44); } }
        RectangleF RepeatRowRect { get { return Ui.RF(356, 452, 174, 26); } }
        RectangleF RepeatSwitchRect { get { return Ui.RF(496, 456, 34, 18); } }
        RectangleF BtnSaveRect { get { return Ui.RF(410, 654, 120, 42); } }
        RectangleF BtnCancelRect { get { return Ui.RF(302, 654, 96, 42); } }
        RectangleF BtnDeleteRect { get { return Ui.RF(30, 654, 118, 42); } }
        RectangleF NameFieldRect { get { return Ui.RF(30, 88, 500, 46); } }
        RectangleF SegRect { get { return Ui.RF(30, 566, 300, 38); } }
        RectangleF DescRect { get { return Ui.RF(30, 610, 330, 18); } }

        RectangleF TimeFieldRect(int i) { return Ui.RF(TimeX[i], 482, 100, 44); }
        RectangleF TimeUpRect(int i) { return Ui.RF(TimeX[i] + 70, 485, 26, 18); }
        RectangleF TimeDownRect(int i) { return Ui.RF(TimeX[i] + 70, 505, 26, 18); }

        RectangleF SwatchRect(int i)
        {
            int col = i % 4, row = i / 4;
            return Ui.RF(356 + col * 40, 566 + row * 34, 28, 28);
        }

        RectangleF DayCell(int row, int col)
        {
            return Ui.RF(30 + col * (500f / 7f), 246 + row * 32, 500f / 7f, 32);
        }

        DateTime HitDay(Point p)
        {
            if (p.X < Ui.S(30) || p.X >= Ui.S(530)) return DateTime.MinValue;
            if (p.Y < Ui.S(246) || p.Y >= Ui.S(246 + 6 * 32)) return DateTime.MinValue;
            int col = (int)((p.X - Ui.S(30)) / (500f / 7f * Ui.Scale));
            int row = (int)((p.Y - Ui.S(246)) / (32f * Ui.Scale));
            if (col < 0 || col > 6 || row < 0 || row > 5) return DateTime.MinValue;
            var first = new DateTime(_viewMonth.Year, _viewMonth.Month, 1);
            int offset = ((int)first.DayOfWeek + 6) % 7;
            int day = row * 7 + col - offset + 1;
            if (day < 1 || day > DateTime.DaysInMonth(_viewMonth.Year, _viewMonth.Month))
                return DateTime.MinValue;
            return new DateTime(_viewMonth.Year, _viewMonth.Month, day);
        }

        int HitSegment(Point p)
        {
            if (!SegRect.Contains(p.X / (float)Ui.Scale, p.Y / (float)Ui.Scale)) return -1;
            int designX = (int)(p.X / Ui.Scale);
            if (designX < 30 || designX >= 330) return -1;
            return Math.Min(2, (designX - 30) / 100);
        }

        bool HitTimeArrow(Point p, out int index, out int delta)
        {
            index = -1; delta = 0;
            for (int i = 0; i < 3; i++)
            {
                if (TimeUpRect(i).Contains(p)) { index = i; delta = 1; return true; }
                if (TimeDownRect(i).Contains(p)) { index = i; delta = -1; return true; }
            }
            return false;
        }

        int HitTimeField(Point p)
        {
            for (int i = 0; i < 3; i++)
            {
                if (TimeFieldRect(i).Contains(p)) return i;
            }
            return -1;
        }

        int HitSwatch(Point p)
        {
            for (int i = 0; i < Ui.Palette.Length; i++)
            {
                if (SwatchRect(i).Contains(p)) return i;
            }
            return -1;
        }

        // ---------- interactions ----------
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mouse = e.Location;
            string hot = "";
            DateTime? day = null;

            if (BtnCloseRect.Contains(e.Location)) hot = "close";
            else if (BtnYearPrevRect.Contains(e.Location)) hot = "yearprev";
            else if (BtnYearNextRect.Contains(e.Location)) hot = "yearnext";
            else if (BtnMonthPrevRect.Contains(e.Location)) hot = "monthprev";
            else if (BtnMonthNextRect.Contains(e.Location)) hot = "monthnext";
            else if (BtnTodayRect.Contains(e.Location)) hot = "today";
            else if (RepeatRowRect.Contains(e.Location)) hot = "repeat";
            else if (BtnNowRect.Contains(e.Location)) hot = "now";
            else if (BtnSaveRect.Contains(e.Location)) hot = "save";
            else if (BtnCancelRect.Contains(e.Location)) hot = "cancel";
            else if (!_isNew && BtnDeleteRect.Contains(e.Location)) hot = "delete";
            else
            {
                int seg = HitSegment(e.Location);
                if (seg >= 0) hot = "seg" + seg;
                else
                {
                    int sw = HitSwatch(e.Location);
                    if (sw >= 0) hot = "sw" + sw;
                    else
                    {
                        int idx; int delta;
                        if (HitTimeArrow(e.Location, out idx, out delta)) hot = "time" + idx + (delta > 0 ? "u" : "d");
                        else if (HitTimeField(e.Location) >= 0) hot = "timefield";
                        else
                        {
                            var d = HitDay(e.Location);
                            if (d != DateTime.MinValue) day = d;
                        }
                    }
                }
            }

            bool changed = hot != _hot || day != _hoverDate;
            _hot = hot;
            _hoverDate = day;
            if (changed) Invalidate();

            if (hot == "timefield") Cursor = Cursors.SizeNS;
            else if (day != null || hot != "") Cursor = Cursors.Hand;
            else Cursor = e.Location.Y < Ui.S(54) ? Cursors.SizeAll : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hot != "" || _hoverDate != null)
            {
                _hot = "";
                _hoverDate = null;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Point p = e.Location;

            if (BtnCloseRect.Contains(p)) { DialogResult = DialogResult.Cancel; Close(); return; }
            if (BtnYearPrevRect.Contains(p)) { ShiftYear(-1); return; }
            if (BtnYearNextRect.Contains(p)) { ShiftYear(1); return; }
            if (BtnMonthPrevRect.Contains(p)) { ShiftMonth(-1); return; }
            if (BtnMonthNextRect.Contains(p)) { ShiftMonth(1); return; }
            if (BtnTodayRect.Contains(p))
            {
                var now = DateTime.Now;
                _selDate = now.Date;
                _viewMonth = new DateTime(now.Year, now.Month, 1);
                Invalidate();
                return;
            }
            if (RepeatRowRect.Contains(p))
            {
                _repeat = !_repeat;
                Invalidate();
                return;
            }
            if (BtnNowRect.Contains(p))
            {
                var now = DateTime.Now;
                _selDate = now.Date;
                _hour = now.Hour; _minute = now.Minute; _second = now.Second;
                _viewMonth = new DateTime(now.Year, now.Month, 1);
                Invalidate();
                return;
            }
            if (BtnSaveRect.Contains(p)) { Commit(); return; }
            if (BtnCancelRect.Contains(p)) { DialogResult = DialogResult.Cancel; Close(); return; }
            if (!_isNew && BtnDeleteRect.Contains(p)) { DeleteItem(); return; }

            int seg = HitSegment(p);
            if (seg >= 0)
            {
                _mode = seg;
                Invalidate();
                return;
            }
            int sw = HitSwatch(p);
            if (sw >= 0)
            {
                _accent = Ui.HexColor(Ui.Palette[sw], Ui.Brand);
                Invalidate();
                return;
            }
            int ti; int td;
            if (HitTimeArrow(p, out ti, out td))
            {
                AdjustTime(ti, td);
                return;
            }
            var day = HitDay(p);
            if (day != DateTime.MinValue)
            {
                _selDate = day;
                Invalidate();
                return;
            }
            if (p.Y < Ui.S(54) && p.X < Ui.S(502)) DragWindow();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int idx = HitTimeField(e.Location);
            if (idx >= 0) AdjustTime(idx, e.Delta > 0 ? 1 : -1);
        }

        void AdjustTime(int index, int delta)
        {
            if (index == 0) _hour = (_hour + delta + 24) % 24;
            else if (index == 1) _minute = (_minute + delta + 60) % 60;
            else _second = (_second + delta + 60) % 60;
            Invalidate();
        }

        void ShiftMonth(int delta)
        {
            var next = _viewMonth.AddMonths(delta);
            if (next.Year < 1900 || next.Year > 2200) return;
            _viewMonth = new DateTime(next.Year, next.Month, 1);
            Invalidate();
        }

        void ShiftYear(int delta)
        {
            int year = _viewMonth.Year + delta;
            if (year < 1900 || year > 2200) return;
            _viewMonth = new DateTime(year, _viewMonth.Month, 1);
            Invalidate();
        }

        void Commit()
        {
            string title = GetTitle();
            if (title.Length == 0) title = L10n.T("未命名日子", "Untitled day");
            if (title.Length > 48) title = title.Substring(0, 48);
            var target = _selDate.Date.AddHours(_hour).AddMinutes(_minute).AddSeconds(_second);
            _item.Title = title;
            _item.Target = EventUtil.ToIso(target);
            _item.Mode = _mode;
            _item.Accent = Ui.HexOf(_accent);
            _item.RepeatYearly = _repeat;
            if (string.IsNullOrEmpty(_item.Created)) _item.Created = EventUtil.ToIso(DateTime.Now);
            ResultItem = _item;
            DialogResult = DialogResult.OK;
            Close();
        }

        void DeleteItem()
        {
            string title = GetTitle();
            if (title.Length == 0) title = L10n.T("这个日子", "this day");
            var r = MessageBox.Show(this, L10n.DeleteConfirm(title), L10n.DeleteTitle,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (r == DialogResult.Yes)
            {
                Deleted = true;
                ResultItem = _item;
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                Commit();
                e.Handled = true;
            }
        }

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        void DragWindow()
        {
            try
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            }
            catch { }
        }

        // ---------- painting ----------
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var full = ClientRectangle;
            using (var br = new LinearGradientBrush(full, Ui.DialogTop, Ui.DialogBottom, 115f))
                g.FillRectangle(br, full);
            Ui.Glow(g, Ui.RF(-120, -160, 460, 460), _accent, Ui.GlowAlpha(20));
            Ui.Glow(g, Ui.RF(340, 500, 460, 460), _accent, Ui.GlowAlpha(8));

            // border
            Ui.StrokeRound(g, new RectangleF(Ui.SF(1), Ui.SF(1), ClientSize.Width - 2, ClientSize.Height - 2),
                Ui.SF(10), Ui.Overlay(18), 1f);

            // header
            using (var f = Ui.FClone(16, false, true))
                Ui.Text(g, _isNew ? L10n.T("新建日子", "New day") : L10n.T("编辑日子", "Edit day"), f, Ui.Ink, Ui.RF(30, 18, 300, 26));
            var cr = BtnCloseRect;
            if (_hot == "close") Ui.FillRound(g, cr, Ui.SF(7), Ui.A(Ui.Danger, 38));
            Ui.IconClose(g, new RectangleF(cr.X + 7, cr.Y + 7, cr.Width - 14, cr.Height - 14),
                _hot == "close" ? Ui.Danger : Ui.Text2, Ui.SF(1.55f));

            DrawLabel(g, L10n.T("名称", "Name"), 30, 66);
            DrawNameField(g);

            // calendar
            var panel = PanelRect;
            Ui.FillRound(g, panel, Ui.SF(16), Ui.Overlay(4));
            Ui.StrokeRound(g, panel, Ui.SF(16), Ui.Overlay(8), 1f);
            DrawNavButton(g, BtnYearPrevRect, "yearprev", true);
            DrawNavButton(g, BtnYearNextRect, "yearnext", false);
            DrawNavButton(g, BtnMonthPrevRect, "monthprev", true);
            DrawNavButton(g, BtnMonthNextRect, "monthnext", false);
            using (var f = Ui.FClone(13.5f, false, true))
            {
                Ui.Text(g, L10n.YearLabel(_viewMonth.Year), f, Ui.Ink,
                    Ui.RF(72, 186, 60, 28), StringAlignment.Center, StringAlignment.Center);
                Ui.Text(g, L10n.MonthLabel(_viewMonth), f, Ui.Ink,
                    Ui.RF(204, 186, 52, 28), StringAlignment.Center, StringAlignment.Center);
            }
            if (_hot == "today") Ui.FillRound(g, BtnTodayRect, Ui.SF(8), Ui.Overlay(8));
            using (var f = Ui.FClone(10))
                Ui.Text(g, L10n.T("今天", "Today"), f, _hot == "today" ? Ui.Ink : Ui.Text2, BtnTodayRect,
                    StringAlignment.Center, StringAlignment.Center);

            using (var f = Ui.FClone(9.5f))
            {
                for (int c = 0; c < 7; c++)
                {
                    var cell = DayCell(0, c);
                    Ui.Text(g, L10n.WeekHeader(c), f, Ui.Text3,
                        new RectangleF(cell.X, Ui.SF(222), cell.Width, Ui.SF(18)),
                        StringAlignment.Center, StringAlignment.Center);
                }
            }

            var first = new DateTime(_viewMonth.Year, _viewMonth.Month, 1);
            int offset = ((int)first.DayOfWeek + 6) % 7;
            int days = DateTime.DaysInMonth(_viewMonth.Year, _viewMonth.Month);
            var today = DateTime.Today;
            for (int d = 1; d <= days; d++)
            {
                int slot = offset + d - 1;
                int row = slot / 7, col = slot % 7;
                var cell = DayCell(row, col);
                float cx = cell.X + cell.Width / 2f;
                float cy = cell.Y + cell.Height / 2f;
                float dia = Ui.SF(30);
                var circle = new RectangleF(cx - dia / 2f, cy - dia / 2f, dia, dia);
                var date = new DateTime(_viewMonth.Year, _viewMonth.Month, d);
                bool sel = date == _selDate;
                bool isToday = date == today;
                bool hover = _hoverDate.HasValue && _hoverDate.Value.Date == date;

                if (sel)
                {
                    Ui.FillRound(g, circle, dia / 2f, _accent);
                }
                else if (hover)
                {
                    Ui.FillRound(g, circle, dia / 2f, Ui.Overlay(12));
                }
                else if (isToday)
                {
                    Ui.StrokeRound(g, circle, dia / 2f, Ui.A(_accent, 130), 1f);
                }
                using (var f = Ui.FClone(11))
                    Ui.Text(g, d.ToString(CultureInfo.InvariantCulture), f,
                        sel ? Ui.OnAccent : isToday ? Ui.AccentInk(_accent) : Ui.Ink,
                        cell, StringAlignment.Center, StringAlignment.Center);
            }

            DrawLabel(g, L10n.T("时间", "Time"), 30, 460);
            using (var f = Ui.FClone(10))
                Ui.Text(g, L10n.T("每年重复", "Repeat yearly"), f, _hot == "repeat" ? Ui.Ink : Ui.Text2,
                    Ui.RF(356, 456, 110, 20));
            Ui.Switch(g, RepeatSwitchRect, _repeat, _accent);
            DrawTimeField(g, 0, _hour, L10n.T("时", "h"));
            DrawTimeField(g, 1, _minute, L10n.T("分", "m"));
            DrawTimeField(g, 2, _second, L10n.T("秒", "s"));
            using (var f = Ui.FClone(14, true))
            {
                Ui.Text(g, ":", f, Ui.Text3, Ui.RF(129, 482, 10, 44), StringAlignment.Center, StringAlignment.Center);
                Ui.Text(g, ":", f, Ui.Text3, Ui.RF(239, 482, 10, 44), StringAlignment.Center, StringAlignment.Center);
            }
            var nowBtn = BtnNowRect;
            if (_hot == "now") Ui.FillRound(g, nowBtn, Ui.SF(12), Ui.Overlay(8));
            else Ui.FillRound(g, nowBtn, Ui.SF(12), Ui.Overlay(3));
            Ui.StrokeRound(g, nowBtn, Ui.SF(12), Ui.Overlay(10), 1f);
            Ui.IconClock(g, new RectangleF(nowBtn.X + Ui.SF(14), nowBtn.Y + Ui.SF(14), Ui.SF(16), Ui.SF(16)),
                _hot == "now" ? Ui.Ink : Ui.Text3, Ui.SF(1.3f));
            using (var f = Ui.FClone(10.5f))
                Ui.Text(g, L10n.T("设为现在", "Set to now"), f, _hot == "now" ? Ui.Ink : Ui.Text2,
                    new RectangleF(nowBtn.X + Ui.SF(38), nowBtn.Y, nowBtn.Width - Ui.SF(42), nowBtn.Height));

            DrawLabel(g, L10n.T("模式", "Mode"), 30, 544);
            DrawSegmented(g);
            using (var f = Ui.FClone(9.5f))
                Ui.Text(g, EventUtil.ModeDescription(_mode), f, Ui.Text3, DescRect);

            DrawLabel(g, L10n.T("颜色", "Color"), 356, 544);
            for (int i = 0; i < Ui.Palette.Length; i++)
            {
                var sr = SwatchRect(i);
                var col = Ui.HexColor(Ui.Palette[i], Ui.Brand);
                bool sel = Ui.HexOf(_accent) == Ui.HexOf(col);
                float dia = Math.Min(sr.Width, sr.Height);
                var circle = new RectangleF(sr.X + (sr.Width - dia) / 2f, sr.Y + (sr.Height - dia) / 2f, dia, dia);
                if (sel || _hot == "sw" + i)
                    Ui.StrokeRound(g, new RectangleF(circle.X - Ui.SF(4), circle.Y - Ui.SF(4),
                        circle.Width + Ui.SF(8), circle.Height + Ui.SF(8)), Ui.SF(17),
                        Ui.A(sel ? (Ui.IsLight ? Ui.Ink : Color.White) : col, sel ? 170 : 80), 1.2f);
                Ui.FillRound(g, circle, dia / 2f, col);
                if (sel)
                    Ui.IconCheck(g, new RectangleF(circle.X + Ui.SF(7), circle.Y + Ui.SF(7),
                        circle.Width - Ui.SF(14), circle.Height - Ui.SF(14)),
                        Ui.OnAccent, Ui.SF(1.6f));
            }

            DrawFooterButtons(g);
        }

        void DrawLabel(Graphics g, string text, float x, float y)
        {
            using (var f = Ui.FClone(10))
                Ui.Text(g, text, f, Ui.Text3, Ui.RF(x, y, 200, 16));
        }

        void DrawNameField(Graphics g)
        {
            var r = NameFieldRect;
            Ui.FillRound(g, r, Ui.SF(14), Ui.Field);
            Ui.StrokeRound(g, r, Ui.SF(14), _titleBox.Focused ? Ui.A(_accent, 190) : Ui.Overlay(11), 1f);
        }

        void DrawNavButton(Graphics g, RectangleF r, string id, bool left)
        {
            if (_hot == id) Ui.FillRound(g, r, Ui.SF(8), Ui.Overlay(9));
            Ui.IconChevron(g, new RectangleF(r.X + 6, r.Y + 6, r.Width - 12, r.Height - 12),
                _hot == id ? Ui.Ink : Ui.Text2, Ui.SF(1.4f), left);
        }

        void DrawTimeField(Graphics g, int index, int value, string unit)
        {
            var r = TimeFieldRect(index);
            var up = TimeUpRect(index);
            var down = TimeDownRect(index);
            bool hotField = _hot == "timefield" || _hot == "time" + index + "u" || _hot == "time" + index + "d";
            Ui.FillRound(g, r, Ui.SF(13), Ui.Field);
            Ui.StrokeRound(g, r, Ui.SF(13), hotField ? Ui.A(_accent, 120) : Ui.Overlay(11), 1f);

            using (var f = Ui.FClone(21, true))
                Ui.Text(g, value.ToString("00", CultureInfo.InvariantCulture), f, Ui.Ink,
                    new RectangleF(r.X + Ui.SF(11), r.Y + Ui.SF(7), Ui.SF(38), Ui.SF(30)),
                    StringAlignment.Near, StringAlignment.Center);
            using (var f = Ui.FClone(9))
                Ui.Text(g, unit, f, Ui.Text3,
                    new RectangleF(r.X + Ui.SF(50), r.Y + Ui.SF(15), Ui.SF(16), Ui.SF(14)));

            bool upHot = _hot == "time" + index + "u";
            bool downHot = _hot == "time" + index + "d";
            if (upHot) Ui.FillRound(g, new RectangleF(up.X - 2, up.Y, up.Width + 4, up.Height), Ui.SF(6), Ui.Overlay(9));
            if (downHot) Ui.FillRound(g, new RectangleF(down.X - 2, down.Y, down.Width + 4, down.Height), Ui.SF(6), Ui.Overlay(9));
            DrawChevron(g, new RectangleF(up.X + 8, up.Y + 4, up.Width - 16, up.Height - 8), upHot ? _accent : Ui.Text3, true);
            DrawChevron(g, new RectangleF(down.X + 8, down.Y + 4, down.Width - 16, down.Height - 8), downHot ? _accent : Ui.Text3, false);
        }

        void DrawChevron(Graphics g, RectangleF r, Color color, bool up)
        {
            using (var p = new Pen(color, Ui.SF(1.3f)))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f;
                if (up)
                    g.DrawLines(p, new PointF[] { new PointF(r.X, r.Bottom), new PointF(cx, r.Y), new PointF(r.Right, r.Bottom) });
                else
                    g.DrawLines(p, new PointF[] { new PointF(r.X, r.Y), new PointF(cx, r.Bottom), new PointF(r.Right, r.Y) });
            }
        }

        void DrawSegmented(Graphics g)
        {
            var track = SegRect;
            Ui.FillRound(g, track, Ui.SF(19), Ui.Overlay(6));
            Ui.StrokeRound(g, track, Ui.SF(19), Ui.Overlay(9), 1f);
            string[] labels = { L10n.ModeName(0), L10n.ModeName(1), L10n.ModeName(2) };
            for (int i = 0; i < 3; i++)
            {
                var seg = Ui.RF(30 + i * 100, 566, 100, 38);
                var pill = new RectangleF(seg.X + Ui.SF(3), seg.Y + Ui.SF(3), seg.Width - Ui.SF(6), seg.Height - Ui.SF(6));
                if (_mode == i)
                    Ui.FillRound(g, pill, Ui.SF(16), _accent);
                else if (_hot == "seg" + i)
                    Ui.FillRound(g, pill, Ui.SF(16), Ui.Overlay(7));
                using (var f = Ui.FClone(11, false, _mode == i))
                    Ui.Text(g, labels[i], f, _mode == i ? Ui.OnAccent : Ui.Text2,
                        seg, StringAlignment.Center, StringAlignment.Center);
            }
        }

        void DrawFooterButtons(Graphics g)
        {
            // delete
            if (!_isNew)
            {
                var del = BtnDeleteRect;
                bool dh = _hot == "delete";
                if (dh) Ui.FillRound(g, del, Ui.SF(12), Ui.A(Ui.Danger, 26));
                Ui.IconTrash(g, new RectangleF(del.X + Ui.SF(12), del.Y + Ui.SF(11), Ui.SF(16), Ui.SF(16)),
                    dh ? Ui.Danger : Ui.A(Ui.Danger, 190), Ui.SF(1.3f));
                using (var f = Ui.FClone(10.5f))
                    Ui.Text(g, L10n.T("删除", "Delete"), f, dh ? Ui.Danger : Ui.A(Ui.Danger, 180),
                        new RectangleF(del.X + Ui.SF(36), del.Y, del.Width - Ui.SF(40), del.Height));
            }

            // cancel
            var cancel = BtnCancelRect;
            bool chh = _hot == "cancel";
            Ui.FillRound(g, cancel, Ui.SF(13), Ui.Overlay(chh ? 12 : 5));
            Ui.StrokeRound(g, cancel, Ui.SF(13), Ui.Overlay(chh ? 20 : 11), 1f);
            using (var f = Ui.FClone(11.5f))
                Ui.Text(g, L10n.T("取消", "Cancel"), f, chh ? Ui.Ink : Ui.Text2, cancel, StringAlignment.Center, StringAlignment.Center);

            // save
            var save = BtnSaveRect;
            bool sh = _hot == "save";
            using (var br = new LinearGradientBrush(save, Ui.Lighten(_accent, sh ? 0.14f : 0.04f),
                sh ? Ui.Lighten(_accent, 0.24f) : _accent, 0f))
                Ui.FillRound(g, save, Ui.SF(13), br);
            using (var f = Ui.FClone(12, false, true))
                Ui.Text(g, L10n.T("保存", "Save"), f, Ui.OnAccent, save, StringAlignment.Center, StringAlignment.Center);
        }
    }
}
