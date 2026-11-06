using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Shiguang
{
    internal sealed class MainForm : Form
    {
        // ---------- state ----------
        readonly ConfigStore _store;
        readonly Timer _clock = new Timer();
        readonly Timer _saveTimer = new Timer();
        EventItem _selected;
        int _scroll;
        bool _loaded;
        bool _closing;
        bool _dim;
        bool _mini;
        bool _autoStart;
        DateTime _lastThemeCheck = DateTime.MinValue;

        Point _mouse;
        HoverKind _hover = HoverKind.None;
        int _hoverCard = -1;
        string _tip;
        DateTime _hoverAt;
        int _lastPaintedSecond = -1;
        bool _tipPainted;

        ResizeEdge _resizeEdge = ResizeEdge.None;
        Point _resizeStartMouse;
        Rectangle _resizeStartBounds;
        Rectangle _normalBounds;

        Rectangle _emptyCta = Rectangle.Empty;

        enum HoverKind
        {
            None, Theme, Mini, Expand, Pin, Min, Close, New, Edit,
            Card, Header, EmptyCta, AutoStart, Language
        }

        enum ResizeEdge { None, Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight }

        const int BaseW = 1100;
        const int BaseH = 720;
        const int SidebarW = 306;
        const int MinW = 1000;
        const int MinH = 700;
        const int MiniW = 380;
        const int MiniH = 230;

        AppConfig Cfg { get { return _store.Config; } }
        EventItem Selected { get { return _selected; } }

        int W { get { return ClientSize.Width; } }
        int H { get { return ClientSize.Height; } }

        // ---------- construction ----------
        public MainForm(ConfigStore store)
        {
            _store = store;
            L10n.Lang = Cfg.Language == 1 ? 1 : 0;
            ThemeManager.Apply((AppTheme)Math.Max(0, Math.Min(2, Cfg.ThemeMode)));
            _mini = Cfg.Mini;

            Text = L10n.T("时光倒数", "Shiguang Countdown");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            BackColor = Ui.BgBottom;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            if (Cfg.Events.Count == 0 && string.IsNullOrEmpty(Cfg.SavedAt))
                CreateWelcomeEvent();

            _selected = Cfg.Events.FirstOrDefault(x => x.Id == Cfg.SelectedId);
            if (_selected == null && Cfg.Events.Count > 0) _selected = Cfg.Events[0];
            if (_selected != null) Cfg.SelectedId = _selected.Id;

            TopMost = Cfg.TopMost;
            _autoStart = Cfg.AutoStart;
            StartupManager.Sync(_autoStart, Application.ExecutablePath);
            _autoStart = StartupManager.IsEnabled(Application.ExecutablePath);
            Cfg.AutoStart = _autoStart;

            PlaceWindow();

            _clock.Interval = 250;
            _clock.Tick += ClockTick;
            _clock.Start();

            _saveTimer.Interval = 700;
            _saveTimer.Tick += SaveTick;

            LocationChanged += (s, e) => { if (_loaded) QueueSave(); };
            Resize += (s, e) =>
            {
                if (_loaded) QueueSave();
                Invalidate();
            };
            Shown += (s, e) => { _loaded = true; Invalidate(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Ui.RoundWindow(this);
        }

        void CreateWelcomeEvent()
        {
            var now = DateTime.Now;
            var item = new EventItem
            {
                Id = EventUtil.NewId(),
                Title = L10n.T("新的一年", "New Year"),
                Target = EventUtil.ToIso(new DateTime(now.Year + 1, 1, 1, 0, 0, 0)),
                Mode = 0,
                Accent = Ui.Palette[2],
                Created = EventUtil.ToIso(now),
                Note = "",
                RepeatYearly = false
            };
            Cfg.Events.Add(item);
            Cfg.SelectedId = item.Id;
            _store.Save();
        }

        Rectangle NormalBoundsFromConfig(Rectangle wa)
        {
            int w = Cfg.HasWinSize ? Cfg.WinW : Ui.S(BaseW);
            int h = Cfg.HasWinSize ? Cfg.WinH : Ui.S(BaseH);
            w = Math.Max(Ui.S(MinW), Math.Min(w, wa.Width));
            h = Math.Max(Ui.S(MinH), Math.Min(h, wa.Height));
            int x = wa.Left + (wa.Width - w) / 2;
            int y = wa.Top + (wa.Height - h) / 2;
            if (Cfg.HasWinPos)
            {
                var want = new Rectangle(Cfg.WinX, Cfg.WinY, w, h);
                if (Ui.IsOnScreen(want)) { x = Cfg.WinX; y = Cfg.WinY; }
            }
            return ClampBounds(new Rectangle(x, y, w, h), wa);
        }

        void PlaceWindow()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            if (_mini)
            {
                int w = Ui.S(MiniW), h = Ui.S(MiniH);
                int x = wa.Right - w - Ui.S(28);
                int y = wa.Top + Ui.S(28);
                if (Cfg.HasMiniPos)
                {
                    var want = new Rectangle(Cfg.MiniX, Cfg.MiniY, w, h);
                    if (Ui.IsOnScreen(want)) { x = Cfg.MiniX; y = Cfg.MiniY; }
                }
                var b = ClampBounds(new Rectangle(x, y, w, h), wa);
                MinimumSize = new Size(w, h);
                MaximumSize = new Size(w, h);
                Bounds = b;
                _normalBounds = NormalBoundsFromConfig(wa);
            }
            else
            {
                var b = NormalBoundsFromConfig(wa);
                MinimumSize = new Size(Ui.S(MinW), Ui.S(MinH));
                MaximumSize = Size.Empty;
                Bounds = b;
                _normalBounds = b;
            }
            Ui.RoundWindow(this);
        }

        static Rectangle ClampBounds(Rectangle b, Rectangle wa)
        {
            if (b.Width > wa.Width) b.Width = wa.Width;
            if (b.Height > wa.Height) b.Height = wa.Height;
            if (b.Right > wa.Right) b.X = wa.Right - b.Width;
            if (b.Bottom > wa.Bottom) b.Y = wa.Bottom - b.Height;
            if (b.X < wa.Left) b.X = wa.Left;
            if (b.Y < wa.Top) b.Y = wa.Top;
            return b;
        }

        void ClockTick(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            var now = DateTime.Now;

            if (ThemeManager.Mode == AppTheme.System &&
                (now - _lastThemeCheck).TotalSeconds >= 2.0)
            {
                _lastThemeCheck = now;
                bool sysLight = ThemeManager.SystemUsesLight();
                if (sysLight != Ui.IsLight)
                {
                    ThemeManager.Apply(AppTheme.System);
                    BackColor = Ui.BgBottom;
                    Invalidate();
                }
            }

            bool need = now.Second != _lastPaintedSecond;
            if (!need && !_tipPainted && _tip != null && _hover != HoverKind.None &&
                (now - _hoverAt).TotalMilliseconds >= 520)
                need = true;
            if (!need) return;
            _lastPaintedSecond = now.Second;
            Invalidate();
        }

        void SaveTick(object sender, EventArgs e)
        {
            _saveTimer.Stop();
            SaveState();
        }

        void QueueSave()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        void SaveState()
        {
            if (IsDisposed) return;
            RememberWindowPosition();
            Cfg.TopMost = TopMost;
            _store.Save();
            Invalidate();
        }

        void RememberWindowPosition()
        {
            if (!_loaded) return;
            var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            if (b.Width <= 0 || b.Height <= 0 || b.X < -20000 || b.Y < -20000) return;
            if (_mini)
            {
                Cfg.MiniX = b.X;
                Cfg.MiniY = b.Y;
                Cfg.HasMiniPos = true;
                Cfg.Mini = true;
            }
            else
            {
                Cfg.WinX = b.X;
                Cfg.WinY = b.Y;
                Cfg.WinW = b.Width;
                Cfg.WinH = b.Height;
                Cfg.HasWinPos = true;
                Cfg.HasWinSize = true;
                Cfg.Mini = false;
            }
            Cfg.TopMost = TopMost;
        }

        // ---------- geometry ----------
        Rectangle SidebarRect { get { return new Rectangle(0, 0, Ui.S(SidebarW), H); } }
        Rectangle ListRect
        {
            get
            {
                int h = Math.Max(Ui.S(120), H - Ui.S(136) - Ui.S(126));
                return new Rectangle(Ui.S(14), Ui.S(136), Ui.S(278), h);
            }
        }
        Rectangle LanguageRect { get { return new Rectangle(Ui.S(246), Ui.S(28), Ui.S(38), Ui.S(22)); } }
        Rectangle AutoRowRect { get { return new Rectangle(Ui.S(22), H - Ui.S(112), Ui.S(262), Ui.S(26)); } }
        Rectangle BtnNewRect { get { return new Rectangle(Ui.S(22), H - Ui.S(82), Ui.S(262), Ui.S(50)); } }
        Rectangle MiniSizeRect { get { return new Rectangle(0, 0, Ui.S(MiniW), Ui.S(MiniH)); } }

        Rectangle TopButton(int fromRight)
        {
            return new Rectangle(W - Ui.S(56 + fromRight * 36), Ui.S(18), Ui.S(32), Ui.S(28));
        }

        Rectangle BtnThemeRect { get { return _mini ? Rectangle.Empty : TopButton(4); } }
        Rectangle BtnMiniRect { get { return _mini ? Rectangle.Empty : TopButton(3); } }
        Rectangle BtnPinRect
        {
            get { return _mini ? new Rectangle(W - Ui.S(112), Ui.S(14), Ui.S(26), Ui.S(26)) : TopButton(2); }
        }
        Rectangle BtnMinRect { get { return _mini ? Rectangle.Empty : TopButton(1); } }
        Rectangle BtnCloseRect
        {
            get { return _mini ? new Rectangle(W - Ui.S(44), Ui.S(14), Ui.S(26), Ui.S(26)) : TopButton(0); }
        }
        Rectangle BtnExpandRect
        {
            get { return _mini ? new Rectangle(W - Ui.S(78), Ui.S(14), Ui.S(26), Ui.S(26)) : Rectangle.Empty; }
        }

        Rectangle BtnEditRect { get { return new Rectangle(W - Ui.S(42) - Ui.S(90), Ui.S(80), Ui.S(90), Ui.S(34)); } }
        Rectangle HeroRect
        {
            get
            {
                int x = Ui.S(SidebarW + 42);
                int y = Ui.S(148);
                int w = Math.Max(Ui.S(200), W - x - Ui.S(42));
                int h = Math.Max(Ui.S(300), H - y - Ui.S(84));
                return new Rectangle(x, y, w, h);
            }
        }

        Rectangle CardRect(int i)
        {
            int h = Ui.S(76), gap = Ui.S(8);
            var list = ListRect;
            return new Rectangle(list.X, list.Y + i * (h + gap) - _scroll, list.Width, h);
        }

        int ScrollMax
        {
            get
            {
                int h = Ui.S(76), gap = Ui.S(8);
                int total = Cfg.Events.Count * (h + gap) - gap;
                return Math.Max(0, total - ListRect.Height);
            }
        }

        void ScrollBy(int delta)
        {
            int max = ScrollMax;
            int next = Math.Max(0, Math.Min(max, _scroll + delta));
            if (next != _scroll)
            {
                _scroll = next;
                Invalidate();
            }
        }

        int HitCard(Point p)
        {
            if (!ListRect.Contains(p)) return -1;
            for (int i = 0; i < Cfg.Events.Count; i++)
            {
                var r = CardRect(i);
                if (r.Bottom < ListRect.Top) continue;
                if (r.Top > ListRect.Bottom) break;
                if (r.Contains(p)) return i;
            }
            return -1;
        }

        bool IsTopButton(Point p)
        {
            return BtnCloseRect.Contains(p) || BtnMinRect.Contains(p) || BtnPinRect.Contains(p) ||
                   BtnMiniRect.Contains(p) || BtnThemeRect.Contains(p) || BtnExpandRect.Contains(p);
        }

        bool InHeaderDragArea(Point p)
        {
            if (_mini)
                return !IsTopButton(p);
            if (p.Y >= Ui.S(64) || p.X < Ui.S(SidebarW)) return false;
            return !IsTopButton(p);
        }

        ResizeEdge HitEdge(Point p)
        {
            if (_mini) return ResizeEdge.None;
            int t = Math.Max(4, Ui.S(6));
            bool l = p.X <= t, r = p.X >= W - t, tp = p.Y <= t, bt = p.Y >= H - t;
            if (tp && l) return ResizeEdge.TopLeft;
            if (tp && r) return ResizeEdge.TopRight;
            if (bt && l) return ResizeEdge.BottomLeft;
            if (bt && r) return ResizeEdge.BottomRight;
            if (l) return ResizeEdge.Left;
            if (r) return ResizeEdge.Right;
            if (tp) return ResizeEdge.Top;
            if (bt) return ResizeEdge.Bottom;
            return ResizeEdge.None;
        }

        Cursor CursorForEdge(ResizeEdge e)
        {
            switch (e)
            {
                case ResizeEdge.Left:
                case ResizeEdge.Right: return Cursors.SizeWE;
                case ResizeEdge.Top:
                case ResizeEdge.Bottom: return Cursors.SizeNS;
                case ResizeEdge.TopLeft:
                case ResizeEdge.BottomRight: return Cursors.SizeNWSE;
                case ResizeEdge.TopRight:
                case ResizeEdge.BottomLeft: return Cursors.SizeNESW;
                default: return Cursors.Default;
            }
        }

        // ---------- interactions ----------
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mouse = e.Location;

            if (_resizeEdge != ResizeEdge.None)
            {
                if ((e.Button & MouseButtons.Left) != 0) DoResize();
                else EndResize();
                return;
            }

            UpdateHover(e.Location);
            if (_resizeEdge == ResizeEdge.None)
            {
                var edge = HitEdge(e.Location);
                if (edge != ResizeEdge.None) Cursor = CursorForEdge(edge);
            }
        }

        void UpdateHover(Point p)
        {
            HoverKind kind = HoverKind.None;
            int card = -1;

            if (_mini)
            {
                if (BtnCloseRect.Contains(p)) kind = HoverKind.Close;
                else if (BtnExpandRect.Contains(p)) kind = HoverKind.Expand;
                else if (BtnPinRect.Contains(p)) kind = HoverKind.Pin;
                else if (InHeaderDragArea(p)) kind = HoverKind.Header;
            }
            else
            {
                if (BtnCloseRect.Contains(p)) kind = HoverKind.Close;
                else if (BtnMinRect.Contains(p)) kind = HoverKind.Min;
                else if (BtnPinRect.Contains(p)) kind = HoverKind.Pin;
                else if (BtnMiniRect.Contains(p)) kind = HoverKind.Mini;
                else if (BtnThemeRect.Contains(p)) kind = HoverKind.Theme;
                else if (LanguageRect.Contains(p)) kind = HoverKind.Language;
                else if (BtnNewRect.Contains(p)) kind = HoverKind.New;
                else if (AutoRowRect.Contains(p)) kind = HoverKind.AutoStart;
                else if (_emptyCta != Rectangle.Empty && _emptyCta.Contains(p)) kind = HoverKind.EmptyCta;
                else if (_selected != null && BtnEditRect.Contains(p)) kind = HoverKind.Edit;
                else if (InHeaderDragArea(p)) kind = HoverKind.Header;
                else
                {
                    card = HitCard(p);
                    if (card >= 0) kind = HoverKind.Card;
                }
            }

            if (kind != _hover || card != _hoverCard)
            {
                _hover = kind;
                _hoverCard = card;
                _tip = BuildTip(kind);
                _hoverAt = DateTime.Now;
                _tipPainted = false;
                Invalidate();
            }

            if (kind == HoverKind.None) Cursor = Cursors.Default;
            else if (kind == HoverKind.Header) Cursor = Cursors.SizeAll;
            else Cursor = Cursors.Hand;
        }

        string BuildTip(HoverKind kind)
        {
            switch (kind)
            {
                case HoverKind.Theme:
                    return L10n.En
                        ? "Theme: " + L10n.ThemeName(ThemeManager.Mode) + " (click to switch)"
                        : "主题：" + L10n.ThemeName(ThemeManager.Mode) + "（点击切换）";
                case HoverKind.Mini: return L10n.T("迷你模式", "Mini mode");
                case HoverKind.Expand: return L10n.T("展开完整窗口", "Expand full window");
                case HoverKind.Pin: return TopMost ? L10n.T("取消置顶", "Unpin") : L10n.T("窗口置顶", "Keep on top");
                case HoverKind.Min: return L10n.T("最小化", "Minimize");
                case HoverKind.Close: return L10n.T("关闭", "Close");
                case HoverKind.New: return L10n.T("新建日子  Ctrl+N", "New day  Ctrl+N");
                case HoverKind.Edit: return L10n.T("编辑这个日子", "Edit this day");
                case HoverKind.EmptyCta: return L10n.T("新建日子  Ctrl+N", "New day  Ctrl+N");
                case HoverKind.AutoStart:
                    return _autoStart ? L10n.T("开机启动：已开启", "Start with Windows: on")
                                      : L10n.T("开机启动：已关闭", "Start with Windows: off");
                case HoverKind.Language:
                    return L10n.En ? "中文" : "Switch to English";
                default: return null;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = HoverKind.None;
            _hoverCard = -1;
            _tip = null;
            _tipPainted = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Point p = e.Location;

            if (!_mini)
            {
                var edge = HitEdge(p);
                if (edge != ResizeEdge.None) { BeginResize(edge); return; }
            }

            if (_mini)
            {
                if (BtnCloseRect.Contains(p)) { Close(); return; }
                if (BtnExpandRect.Contains(p)) { ExitMini(); return; }
                if (BtnPinRect.Contains(p)) { ToggleTopMost(); return; }
                DragWindow();
                return;
            }

            if (BtnCloseRect.Contains(p)) { Close(); return; }
            if (BtnMinRect.Contains(p)) { WindowState = FormWindowState.Minimized; return; }
            if (BtnPinRect.Contains(p)) { ToggleTopMost(); return; }
            if (BtnThemeRect.Contains(p)) { CycleTheme(); return; }
            if (BtnMiniRect.Contains(p)) { EnterMini(); return; }
            if (LanguageRect.Contains(p)) { ToggleLanguage(); return; }
            if (BtnNewRect.Contains(p) || (_emptyCta != Rectangle.Empty && _emptyCta.Contains(p)))
            {
                OpenNew();
                return;
            }
            if (AutoRowRect.Contains(p))
            {
                ToggleAutoStart();
                return;
            }
            if (_selected != null && BtnEditRect.Contains(p)) { OpenEdit(); return; }

            int idx = HitCard(p);
            if (idx >= 0)
            {
                var ev = Cfg.Events[idx];
                if (_selected == null || ev.Id != _selected.Id)
                {
                    _selected = ev;
                    Cfg.SelectedId = ev.Id;
                    _store.Save();
                    Invalidate();
                }
                return;
            }

            if (InHeaderDragArea(p)) DragWindow();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_resizeEdge != ResizeEdge.None) EndResize();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (_mini)
            {
                if (!IsTopButton(e.Location)) ExitMini();
                return;
            }
            int idx = HitCard(e.Location);
            if (idx >= 0)
            {
                if (_selected == null || Cfg.Events[idx].Id != _selected.Id)
                {
                    _selected = Cfg.Events[idx];
                    Cfg.SelectedId = _selected.Id;
                    _store.Save();
                }
                OpenEdit();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!_mini && ListRect.Contains(e.Location))
                ScrollBy(-Math.Sign(e.Delta) * Ui.S(66));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control && e.KeyCode == Keys.N)
            {
                if (_mini) ExitMini();
                OpenNew();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape && _mini)
            {
                ExitMini();
                e.Handled = true;
            }
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (_resizeEdge != ResizeEdge.None && !Capture) EndResize();
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

        // ---------- resizing ----------
        void BeginResize(ResizeEdge edge)
        {
            _resizeEdge = edge;
            _resizeStartMouse = Cursor.Position;
            _resizeStartBounds = Bounds;
            _hover = HoverKind.None;
            _tip = null;
            _tipPainted = false;
            Capture = true;
            Cursor = CursorForEdge(edge);
        }

        void DoResize()
        {
            if (_resizeEdge == ResizeEdge.None) return;
            var pos = Cursor.Position;
            int dx = pos.X - _resizeStartMouse.X;
            int dy = pos.Y - _resizeStartMouse.Y;
            var b = _resizeStartBounds;
            int left = b.Left, top = b.Top, right = b.Right, bottom = b.Bottom;
            int minW = Ui.S(MinW), minH = Ui.S(MinH);

            if (_resizeEdge == ResizeEdge.Left || _resizeEdge == ResizeEdge.TopLeft || _resizeEdge == ResizeEdge.BottomLeft)
                left = Math.Min(b.Right - minW, b.Left + dx);
            if (_resizeEdge == ResizeEdge.Right || _resizeEdge == ResizeEdge.TopRight || _resizeEdge == ResizeEdge.BottomRight)
                right = Math.Max(b.Left + minW, b.Right + dx);
            if (_resizeEdge == ResizeEdge.Top || _resizeEdge == ResizeEdge.TopLeft || _resizeEdge == ResizeEdge.TopRight)
                top = Math.Min(b.Bottom - minH, b.Top + dy);
            if (_resizeEdge == ResizeEdge.Bottom || _resizeEdge == ResizeEdge.BottomLeft || _resizeEdge == ResizeEdge.BottomRight)
                bottom = Math.Max(b.Top + minH, b.Bottom + dy);

            var nr = new Rectangle(left, top, Math.Max(minW, right - left), Math.Max(minH, bottom - top));
            if (nr != Bounds) Bounds = nr;
        }

        void EndResize()
        {
            if (_resizeEdge == ResizeEdge.None) return;
            _resizeEdge = ResizeEdge.None;
            try { Capture = false; } catch { }
            RememberWindowPosition();
            QueueSave();
            Invalidate();
        }

        // ---------- mode / actions ----------
        void ToggleTopMost()
        {
            TopMost = !TopMost;
            Cfg.TopMost = TopMost;
            _store.Save();
            Invalidate();
        }

        void CycleTheme()
        {
            var next = ThemeManager.Cycle(ThemeManager.Mode);
            ThemeManager.Apply(next);
            Cfg.ThemeMode = (int)next;
            BackColor = Ui.BgBottom;
            _store.Save();
            Invalidate();
        }

        void ToggleLanguage()
        {
            L10n.Lang = L10n.En ? 0 : 1;
            Cfg.Language = L10n.Lang;
            Text = L10n.T("时光倒数", "Shiguang Countdown");
            _tip = BuildTip(HoverKind.Language);
            _store.Save();
            Invalidate();
        }

        void ToggleAutoStart()
        {
            bool want = !_autoStart;
            bool ok = StartupManager.SetEnabled(Application.ExecutablePath, want);
            if (ok)
            {
                _autoStart = StartupManager.IsEnabled(Application.ExecutablePath);
                Cfg.AutoStart = _autoStart;
                _store.Save();
                Invalidate();
            }
        }

        void EnterMini()
        {
            if (_mini) return;
            RememberWindowPosition();
            _normalBounds = Bounds;
            Cfg.WinX = Bounds.X;
            Cfg.WinY = Bounds.Y;
            Cfg.WinW = Bounds.Width;
            Cfg.WinH = Bounds.Height;
            Cfg.HasWinPos = true;
            Cfg.HasWinSize = true;

            _mini = true;
            Cfg.Mini = true;
            int w = Ui.S(MiniW), h = Ui.S(MiniH);
            var wa = Screen.FromControl(this).WorkingArea;
            int x = Math.Max(wa.Left + Ui.S(12), Math.Min(Bounds.Left, wa.Right - w - Ui.S(12)));
            int y = Math.Max(wa.Top + Ui.S(12), Math.Min(Bounds.Top, wa.Bottom - h - Ui.S(12)));
            MinimumSize = new Size(w, h);
            MaximumSize = new Size(w, h);
            Bounds = new Rectangle(x, y, w, h);
            Cfg.MiniX = x;
            Cfg.MiniY = y;
            Cfg.HasMiniPos = true;
            _hover = HoverKind.None;
            _tip = null;
            _store.Save();
            Invalidate();
        }

        void ExitMini()
        {
            if (!_mini) return;
            Cfg.MiniX = Left;
            Cfg.MiniY = Top;
            Cfg.HasMiniPos = true;
            _mini = false;
            Cfg.Mini = false;

            var wa = Screen.FromControl(this).WorkingArea;
            var b = _normalBounds;
            if (b.Width < Ui.S(MinW) || b.Height < Ui.S(MinH) || !Ui.IsOnScreen(b))
                b = NormalBoundsFromConfig(wa);
            b = ClampBounds(b, wa);
            MinimumSize = new Size(Ui.S(MinW), Ui.S(MinH));
            MaximumSize = Size.Empty;
            Bounds = b;
            _hover = HoverKind.None;
            _tip = null;
            _store.Save();
            Invalidate();
        }

        // ---------- editor / actions ----------
        void OpenNew()
        {
            if (_mini) ExitMini();
            var now = DateTime.Now;
            var item = new EventItem
            {
                Id = EventUtil.NewId(),
                Title = "",
                Target = EventUtil.ToIso(now.Date.AddDays(1).AddHours(9)),
                Mode = 0,
                Accent = Ui.Palette[Cfg.Events.Count % Ui.Palette.Length],
                Created = EventUtil.ToIso(now),
                Note = "",
                RepeatYearly = false
            };
            using (var dlg = new EditDialog(item, true))
            {
                dlg.TopMost = TopMost;
                var res = ShowEditor(dlg);
                if (res == DialogResult.OK && dlg.ResultItem != null)
                {
                    Cfg.Events.Insert(0, dlg.ResultItem);
                    _selected = dlg.ResultItem;
                    Cfg.SelectedId = dlg.ResultItem.Id;
                    _scroll = 0;
                    _store.Save();
                    Invalidate();
                }
            }
        }

        DialogResult ShowEditor(EditDialog dlg)
        {
            _dim = true;
            Invalidate();
            Update();
            try
            {
                return dlg.ShowDialog(this);
            }
            finally
            {
                _dim = false;
                Invalidate();
            }
        }

        void OpenEdit()
        {
            if (_selected == null) return;
            if (_mini) ExitMini();
            var clone = EventUtil.Clone(_selected);
            using (var dlg = new EditDialog(clone, false))
            {
                dlg.TopMost = TopMost;
                var res = ShowEditor(dlg);
                if (res != DialogResult.OK) return;

                if (dlg.Deleted)
                {
                    Cfg.Events.RemoveAll(x => x.Id == _selected.Id);
                    _selected = Cfg.Events.FirstOrDefault();
                    Cfg.SelectedId = _selected == null ? "" : _selected.Id;
                }
                else if (dlg.ResultItem != null)
                {
                    int idx = Cfg.Events.FindIndex(x => x.Id == dlg.ResultItem.Id);
                    if (idx >= 0) Cfg.Events[idx] = dlg.ResultItem;
                    _selected = dlg.ResultItem;
                    Cfg.SelectedId = _selected.Id;
                }
                _scroll = Math.Max(0, Math.Min(_scroll, ScrollMax));
                _store.Save();
                Invalidate();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_closing)
            {
                _closing = true;
                _clock.Stop();
                _saveTimer.Stop();
                RememberWindowPosition();
                Cfg.TopMost = TopMost;
                _store.Save();
                _store.PrepareExit();
            }
            base.OnFormClosing(e);
        }

        // ---------- painting ----------
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var now = DateTime.Now;
            if (_mini)
            {
                DrawMini(g, now);
                DrawTooltip(g);
                return;
            }

            DrawBackground(g);
            DrawSidebar(g, now);
            DrawMain(g, now);
            if (_dim)
            {
                using (var b = new SolidBrush(Color.FromArgb(Ui.IsLight ? 90 : 135, 3, 7, 14)))
                    g.FillRectangle(b, ClientRectangle);
            }
            else
            {
                DrawTooltip(g);
            }
        }

        void DrawBackground(Graphics g)
        {
            var full = ClientRectangle;
            using (var br = new LinearGradientBrush(full, Ui.BgTop, Ui.BgBottom, 115f))
                g.FillRectangle(br, full);
            Ui.Glow(g, new RectangleF(W * 0.60f, -H * 0.36f, W * 0.75f, W * 0.75f), Ui.Brand2,
                Ui.GlowAlpha(18));
            Ui.Glow(g, new RectangleF(W * 0.18f, H * 0.75f, W * 0.55f, H * 0.85f), Ui.Brand,
                Ui.GlowAlpha(9));
        }

        void DrawSidebar(Graphics g, DateTime now)
        {
            var side = SidebarRect;
            using (var br = new LinearGradientBrush(side, Ui.SidebarTop, Ui.SidebarBottom, 90f))
                g.FillRectangle(br, side);
            using (var p = new Pen(Ui.Overlay(10), 1f))
                g.DrawLine(p, side.Right - 1, 0, side.Right - 1, side.Height);

            // logo
            var logo = Ui.RF(26, 24, 40, 40);
            using (var br = new LinearGradientBrush(logo, Ui.Brand, Ui.Brand2, 45f))
                Ui.FillRound(g, logo, Ui.SF(12), br);
            var clockRect = new RectangleF(logo.X + Ui.SF(10), logo.Y + Ui.SF(9), Ui.SF(20), Ui.SF(20));
            using (var pen = new Pen(Ui.A(Color.White, 240), Ui.SF(1.7f)))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawEllipse(pen, clockRect);
                float cx = clockRect.X + clockRect.Width / 2f, cy = clockRect.Y + clockRect.Height / 2f;
                g.DrawLine(pen, cx, cy, cx, cy - Ui.SF(5.2f));
                g.DrawLine(pen, cx, cy, cx + Ui.SF(4.4f), cy + Ui.SF(2.4f));
            }
            Ui.FillRound(g, new RectangleF(clockRect.X + clockRect.Width / 2f - Ui.SF(1.2f),
                clockRect.Y + clockRect.Height / 2f - Ui.SF(1.2f), Ui.SF(2.4f), Ui.SF(2.4f)),
                Ui.SF(1.2f), Ui.A(Color.White, 230));

            using (var brandFont = Ui.FClone(20, false, true))
                Ui.Text(g, L10n.T("时光倒数", "SHIGUANG"), brandFont, Ui.Ink, Ui.RF(78, 27, 160, 26));
            using (var small = Ui.FClone(8))
                Ui.Spaced(g, "COUNTDOWN  CALENDAR", small, Ui.Text3, Ui.RF(79, 55, 210, 14), Ui.SF(1.3f));

            var lr = LanguageRect;
            if (_hover == HoverKind.Language) Ui.FillRound(g, lr, Ui.SF(11), Ui.Overlay(10));
            else Ui.FillRound(g, lr, Ui.SF(11), Ui.Overlay(5));
            Ui.StrokeRound(g, lr, Ui.SF(11), Ui.Overlay(12), 1f);
            using (var lf = Ui.FClone(9, false, true))
                Ui.Text(g, L10n.LangButton, lf, _hover == HoverKind.Language ? Ui.Ink : Ui.Text2,
                    lr, StringAlignment.Center, StringAlignment.Center);

            using (var secFont = Ui.FClone(11))
                Ui.Text(g, L10n.T("我的日子", "MY DAYS"), secFont, Ui.Text2, Ui.RF(26, 100, 160, 18));
            using (var cntFont = Ui.FClone(9.5f))
                Ui.Text(g, L10n.EventCount(Cfg.Events.Count), cntFont, Ui.Text3, Ui.RF(190, 100, 90, 18), StringAlignment.Far);
            using (var pen = new Pen(Ui.Overlay(7), 1f))
                g.DrawLine(pen, 26, 126, 280, 126);

            var list = ListRect;
            var clip = g.Save();
            g.SetClip(list);
            for (int i = 0; i < Cfg.Events.Count; i++)
            {
                var r = CardRect(i);
                if (r.Bottom < list.Top || r.Top > list.Bottom) continue;
                DrawEventCard(g, Cfg.Events[i], i, r, now);
            }
            g.Restore(clip);

            if (ScrollMax > 0) DrawScrollbar(g);

            DrawAutoStartRow(g);

            // new button
            var nb = BtnNewRect;
            bool nHover = _hover == HoverKind.New;
            var nb1 = nHover ? Ui.Lighten(Ui.Brand, 0.10f) : Ui.Brand;
            var nb2 = nHover ? Ui.Lighten(Ui.Brand2, 0.10f) : Ui.Brand2;
            using (var br = new LinearGradientBrush(nb, nb1, nb2, 0f))
                Ui.FillRound(g, nb, Ui.SF(15), br);
            Ui.IconPlus(g, new RectangleF(nb.X + Ui.SF(22), nb.Y + Ui.SF(17), Ui.SF(16), Ui.SF(16)),
                Ui.OnAccent, Ui.SF(1.9f));
            using (var f = Ui.FClone(14, false, true))
                Ui.Text(g, L10n.T("新建日子", "New day"), f, Ui.OnAccent, Ui.RF(nb.X + 48, nb.Y, 180, nb.Height));

            // footer status
            string status = _store.Dirty ? L10n.T("已自动保存 · 退出后写回 EXE", "Saved · writes to EXE on exit") : L10n.T("配置已内嵌 · 自动保存", "Stored inside EXE · autosaved");
            using (var f = Ui.FClone(9))
            {
                var sz = Ui.Measure(g, status, f);
                float total = sz.Width + Ui.SF(12);
                float sx = (side.Width - total) / 2f;
                var dot = new RectangleF(sx, H - Ui.SF(18), Ui.SF(5), Ui.SF(5));
                Ui.FillRound(g, dot, Ui.SF(2.5f), _store.Dirty ? Ui.Warn : Ui.Success);
                Ui.Text(g, status, f, Ui.Text3, new RectangleF(sx + Ui.SF(12), H - Ui.SF(26), sz.Width + 2, Ui.SF(18)));
            }
        }

        void DrawAutoStartRow(Graphics g)
        {
            var row = AutoRowRect;
            bool hov = _hover == HoverKind.AutoStart;
            if (hov) Ui.FillRound(g, row, Ui.SF(9), Ui.Overlay(6));
            using (var f = Ui.FClone(9.5f))
                Ui.Text(g, L10n.T("开机启动", "Start with Windows"), f, hov ? Ui.Ink : Ui.Text3,
                    new RectangleF(row.X + Ui.SF(8), row.Y, Ui.SF(120), row.Height));
            var sw = new RectangleF(row.Right - Ui.SF(42), row.Y + Ui.SF(4), Ui.SF(34), Ui.SF(18));
            Ui.Switch(g, sw, _autoStart, Ui.AccentInk(Ui.Brand));
        }

        void DrawScrollbar(Graphics g)
        {
            int max = ScrollMax;
            if (max <= 0) return;
            var list = ListRect;
            float trackX = list.Right + Ui.SF(1);
            float trackY = list.Top + Ui.SF(4);
            float trackH = list.Height - Ui.SF(8);
            Ui.FillRound(g, new RectangleF(trackX, trackY, Ui.SF(3), trackH), Ui.SF(1.5f), Ui.Overlay(7));
            float thumbH = Math.Max(Ui.SF(26), trackH * list.Height / (float)(list.Height + max));
            float thumbY = trackY + (trackH - thumbH) * (_scroll / (float)max);
            Ui.FillRound(g, new RectangleF(trackX, thumbY, Ui.SF(3), thumbH), Ui.SF(1.5f), Ui.Overlay(28));
        }

        void DrawEventCard(Graphics g, EventItem ev, int index, Rectangle r, DateTime now)
        {
            bool sel = _selected != null && ev.Id == _selected.Id;
            bool hov = _hover == HoverKind.Card && _hoverCard == index;
            var accent = EventUtil.AccentOf(ev, index);
            var accentInk = Ui.AccentInk(accent);
            float radius = Ui.SF(14);

            if (sel)
            {
                Ui.FillRound(g, r, radius, Ui.A(accent, Ui.IsLight ? 22 : 27));
                Ui.StrokeRound(g, new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1),
                    radius, Ui.A(accent, 95), 1f);
                Ui.FillRound(g, new RectangleF(r.X + 9, r.Y + 20, Ui.SF(3), Ui.SF(36)), Ui.SF(1.5f), accent);
            }
            else
            {
                if (hov)
                {
                    Ui.FillRound(g, r, radius, Ui.Overlay(10));
                    Ui.StrokeRound(g, new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1),
                        radius, Ui.Overlay(16), 1f);
                }
                Ui.FillRound(g, new RectangleF(r.X + 9, r.Y + 20, Ui.SF(3), Ui.SF(36)),
                    Ui.SF(1.5f), Ui.A(accent, hov ? 150 : 90));
            }

            var original = EventUtil.TargetOf(ev);
            var target = ev.RepeatYearly && ev.Mode != 2
                ? EventUtil.NextOccurrence(original, now)
                : original;
            bool future = target > now;
            var span = future ? target - now : now - target;
            long totalSec = future ? (long)Math.Ceiling(span.TotalSeconds) : (long)Math.Floor(span.TotalSeconds);
            if (totalSec < 0) totalSec = 0;
            int days = (int)(totalSec / 86400);
            string unit = L10n.DaysUnit(future);

            float tx = r.X + 20, tw = r.Width - 96;
            using (var f = Ui.FClone(13))
                Ui.Text(g, string.IsNullOrEmpty(ev.Title) ? L10n.T("未命名日子", "Untitled day") : ev.Title,
                    f, sel ? Ui.Ink : Ui.Ink,
                    new RectangleF(tx, r.Y + 14, tw, 20), StringAlignment.Near, StringAlignment.Center, true);
            string dateText = EventUtil.ShortDate(target) + (ev.RepeatYearly ? L10n.T(" · 每年", " · yearly") : "");
            using (var f = Ui.FClone(9.5f))
                Ui.Text(g, dateText, f, sel ? Ui.Text2 : Ui.Text3,
                    new RectangleF(tx, r.Y + 41, tw, 16), StringAlignment.Near, StringAlignment.Center, true);

            var numRect = new RectangleF(r.Right - 84, r.Y + 11, 66, 32);
            string word = null;
            if (target.Date == now.Date) word = L10n.Today;
            else if (future && target.Date == now.Date.AddDays(1)) word = L10n.Tomorrow;
            else if (!future && target.Date == now.Date.AddDays(-1)) word = L10n.Yesterday;

            if (word != null)
            {
                using (var tf = Ui.FClone(12, false, true))
                    Ui.Text(g, word, tf, sel ? accentInk : Ui.Text2, numRect, StringAlignment.Far, StringAlignment.Center);
            }
            else
            {
                string ds = days.ToString(CultureInfo.InvariantCulture);
                float dsize = 25f;
                Font df = Ui.F(dsize, true);
                while (dsize > 14f && Ui.Measure(g, ds, df).Width > Ui.SF(62)) { dsize -= 1.5f; df = Ui.F(dsize, true); }
                Ui.Text(g, ds, df, sel ? accentInk : Ui.Ink, numRect, StringAlignment.Far, StringAlignment.Center);
                using (var f = Ui.FClone(9))
                    Ui.Text(g, unit, f, Ui.Text3, new RectangleF(r.Right - 84, r.Y + 44, 66, 16), StringAlignment.Far, StringAlignment.Center);
            }
        }

        void DrawMain(Graphics g, DateTime now)
        {
            DrawWindowButtons(g);

            if (_selected == null)
            {
                DrawEmpty(g);
                DrawMainFooter(g);
                return;
            }

            var ev = _selected;
            var original = EventUtil.TargetOf(ev);
            var target = EventUtil.EffectiveTarget(ev, now);
            int idx = Cfg.Events.IndexOf(ev);
            var accent = EventUtil.AccentOf(ev, idx < 0 ? 0 : idx);
            var accentInk = Ui.AccentInk(accent);

            using (var f = Ui.FClone(26, false, true))
                Ui.Text(g, string.IsNullOrEmpty(ev.Title) ? L10n.T("未命名日子", "Untitled day") : ev.Title,
                    f, Ui.Ink, new RectangleF(Ui.SF(348), Ui.SF(66),
                        Math.Max(Ui.SF(180), BtnEditRect.X - Ui.SF(348) - Ui.SF(18)), Ui.SF(40)),
                    StringAlignment.Near, StringAlignment.Center, true);

            var human = EventUtil.Human(target);
            using (var f = Ui.FClone(12))
            {
                var sz = Ui.Measure(g, human, f);
                Ui.Text(g, human, f, Ui.Text2, new RectangleF(Ui.SF(348), Ui.SF(110), Math.Min(sz.Width, Ui.SF(390)) + 2, Ui.SF(22)));
                float cx = Ui.SF(348) + Math.Min(sz.Width, Ui.SF(390)) + Ui.SF(14);
                string label = EventUtil.ModeName(ev.Mode);
                using (var mf = Ui.FClone(9.5f))
                {
                    var ms = Ui.Measure(g, label, mf);
                    float chipW = ms.Width + Ui.SF(28);
                    var chip = new RectangleF(cx, Ui.SF(111), chipW, Ui.SF(20));
                    Ui.FillRound(g, chip, Ui.SF(10), Ui.Overlay(6));
                    Ui.StrokeRound(g, chip, Ui.SF(10), Ui.Overlay(11), 1f);
                    Ui.FillRound(g, new RectangleF(chip.X + Ui.SF(10), chip.Y + Ui.SF(8.5f), Ui.SF(3), Ui.SF(3)),
                        Ui.SF(1.5f), accent);
                    Ui.Text(g, label, mf, Ui.Text2,
                        new RectangleF(chip.X + Ui.SF(19), chip.Y, chipW - Ui.SF(22), chip.Height));
                    if (ev.RepeatYearly)
                    {
                        float rx = chip.Right + Ui.SF(8);
                        var rs = Ui.Measure(g, L10n.T("每年", "yearly"), mf);
                        var rchip = new RectangleF(rx, Ui.SF(111), rs.Width + Ui.SF(20), Ui.SF(20));
                        Ui.FillRound(g, rchip, Ui.SF(10), Ui.A(accent, Ui.IsLight ? 30 : 34));
                        Ui.StrokeRound(g, rchip, Ui.SF(10), Ui.A(accent, 90), 1f);
                        Ui.Text(g, L10n.T("每年", "yearly"), mf, accentInk, new RectangleF(rchip.X, rchip.Y, rchip.Width, rchip.Height),
                            StringAlignment.Center, StringAlignment.Center);
                    }
                }
            }

            // edit button
            var eb = BtnEditRect;
            bool eh = _hover == HoverKind.Edit;
            Ui.FillRound(g, eb, Ui.SF(17), Ui.Overlay(eh ? 13 : 7));
            Ui.StrokeRound(g, new RectangleF(eb.X + 0.5f, eb.Y + 0.5f, eb.Width - 1, eb.Height - 1),
                Ui.SF(17), Ui.Overlay(eh ? 24 : 16), 1f);
            Ui.IconPencil(g, new RectangleF(eb.X + Ui.SF(15), eb.Y + Ui.SF(11), Ui.SF(13), Ui.SF(13)),
                eh ? Ui.Ink : Ui.Text2, Ui.SF(1.2f));
            using (var f = Ui.FClone(11.5f))
                Ui.Text(g, L10n.T("编辑", "Edit"), f, eh ? Ui.Ink : Ui.Text2,
                    new RectangleF(eb.X + Ui.SF(33), eb.Y, eb.Width - Ui.SF(38), eb.Height));

            DrawHero(g, ev, original, target, accent, now);
            DrawMainFooter(g);
        }

        void DrawWindowButtons(Graphics g)
        {
            if (_mini)
            {
                DrawWinButton(g, BtnExpandRect, HoverKind.Expand, false,
                    (gr, rr, color) => Ui.IconExpand(gr, rr, color, Ui.SF(1.5f)));
                DrawWinButton(g, BtnPinRect, HoverKind.Pin, TopMost,
                    (gr, rr, color) => Ui.IconPin(gr, rr, color, Ui.SF(1.5f)));
                DrawWinButton(g, BtnCloseRect, HoverKind.Close, false,
                    (gr, rr, color) => Ui.IconClose(gr, rr, color, Ui.SF(1.6f)));
                return;
            }

            var themeIcon = new Action<Graphics, RectangleF, Color>((gr, rr, color) =>
            {
                if (ThemeManager.Mode == AppTheme.Dark) Ui.IconMoon(gr, rr, color, Ui.SF(1.5f));
                else if (ThemeManager.Mode == AppTheme.Light) Ui.IconSun(gr, rr, color, Ui.SF(1.4f));
                else Ui.IconMonitor(gr, rr, color, Ui.SF(1.4f));
            });
            DrawWinButton(g, BtnThemeRect, HoverKind.Theme, false, themeIcon);
            DrawWinButton(g, BtnMiniRect, HoverKind.Mini, false,
                (gr, rr, color) => Ui.IconMini(gr, rr, color, Ui.SF(1.5f)));
            DrawWinButton(g, BtnPinRect, HoverKind.Pin, TopMost,
                (gr, rr, color) => Ui.IconPin(gr, rr, color, Ui.SF(1.5f)));
            DrawWinButton(g, BtnMinRect, HoverKind.Min, false,
                (gr, rr, color) => Ui.IconMinus(gr, rr, color, Ui.SF(1.5f)));
            DrawWinButton(g, BtnCloseRect, HoverKind.Close, false,
                (gr, rr, color) => Ui.IconClose(gr, rr, color, Ui.SF(1.6f)));
        }

        void DrawWinButton(Graphics g, Rectangle r, HoverKind kind, bool active,
            Action<Graphics, RectangleF, Color> icon)
        {
            if (r.Width <= 0) return;
            bool hov = _hover == kind;
            var fill = Color.Transparent;
            if (kind == HoverKind.Close && hov) fill = Ui.A(Ui.Danger, 38);
            else if (active) fill = Ui.A(Ui.Brand, Ui.IsLight ? 28 : 36);
            else if (hov) fill = Ui.Overlay(10);
            if (fill.A > 0) Ui.FillRound(g, r, Ui.SF(8), fill);
            var color = kind == HoverKind.Close ? (hov ? Ui.Danger : Ui.Text2)
                : active ? Ui.AccentInk(Ui.Brand) : (hov ? Ui.Ink : Ui.Text2);
            icon(g, new RectangleF(r.X + r.Width * 0.23f, r.Y + r.Height * 0.23f, r.Width * 0.54f, r.Height * 0.54f), color);
        }

        void DrawHero(Graphics g, EventItem ev, DateTime original, DateTime target, Color accent, DateTime now)
        {
            var hero = HeroRect;
            float radius = Ui.SF(24);
            var state = g.Save();
            using (var path = Ui.Round(hero, radius))
            {
                g.SetClip(path);
                using (var br = new LinearGradientBrush(hero, Ui.CardTop, Ui.CardBottom, 108f))
                    g.FillRectangle(br, hero);
                Ui.Glow(g, new RectangleF(hero.Right - Ui.SF(300), hero.Top - Ui.SF(170),
                    Ui.SF(440), Ui.SF(440)), accent, Ui.GlowAlpha(30));
                Ui.Glow(g, new RectangleF(hero.Left - Ui.SF(240), hero.Bottom - Ui.SF(180),
                    Ui.SF(420), Ui.SF(420)), accent, Ui.GlowAlpha(10));
                g.ResetClip();

                using (var pen = new Pen(Ui.Overlay(14), 1f))
                    g.DrawPath(pen, path);
            }
            g.Restore(state);

            bool future = target > now;
            bool repeatAnnual = ev.RepeatYearly && ev.Mode != 2;
            bool annualCycle = repeatAnnual && original <= now;

            string chip;
            if (repeatAnnual)
                chip = (L10n.En ? "Until " + EventUtil.Human(target) : "距离 " + EventUtil.Human(target) + " 还有")
                    + " · " + EventUtil.AnniversaryLabel(original, target);
            else if (future)
                chip = L10n.En ? "Until " + EventUtil.Human(target) : "距离 " + EventUtil.Human(target) + " 还有";
            else if (ev.Mode == 1)
                chip = L10n.En ? "Reached " + EventUtil.Human(target) : "目标 " + EventUtil.Human(target) + " 已到达";
            else
                chip = L10n.En ? "Since " + EventUtil.Human(target) : "自 " + EventUtil.Human(target) + " 起 已经过去";
            if (!repeatAnnual && ev.RepeatYearly)
                chip += L10n.T(" · 每年", " · yearly");

            using (var cf = Ui.FClone(11))
            {
                var sz = Ui.Measure(g, chip, cf);
                float chipW = Math.Min(sz.Width + Ui.SF(34), hero.Width - Ui.SF(80));
                float chipX = hero.X + (hero.Width - chipW) / 2f;
                var chipRect = new RectangleF(chipX, hero.Y + Ui.SF(28), Math.Max(Ui.SF(120), chipW), Ui.SF(30));
                Ui.FillRound(g, chipRect, Ui.SF(15), Ui.Overlay(6));
                Ui.StrokeRound(g, chipRect, Ui.SF(15), Ui.Overlay(11), 1f);
                Ui.FillRound(g, new RectangleF(chipRect.X + Ui.SF(13), chipRect.Y + Ui.SF(12.5f),
                    Ui.SF(5), Ui.SF(5)), Ui.SF(2.5f), accent);
                var textRect = new RectangleF(chipRect.X + Ui.SF(24), chipRect.Y,
                    chipRect.Width - Ui.SF(32), chipRect.Height);
                Ui.Text(g, chip, cf, Ui.Text2, textRect, StringAlignment.Center, StringAlignment.Center, true);
            }

            var span = future ? target - now : now - target;
            long totalSec = future ? (long)Math.Ceiling(span.TotalSeconds) : (long)Math.Floor(span.TotalSeconds);
            if (totalSec < 0) totalSec = 0;
            int days = (int)(totalSec / 86400);
            int rem = (int)(totalSec % 86400);
            int hours = rem / 3600;
            int minutes = (rem % 3600) / 60;
            int seconds = rem % 60;

            string dayText = days.ToString(CultureInfo.InvariantCulture);
            float fontSize = 142f;
            Font dayFont = Ui.F(fontSize, true, false, true);
            while (fontSize > 68f && Ui.Measure(g, dayText, dayFont).Width > Math.Max(Ui.SF(260), hero.Width * 0.52f))
            {
                fontSize -= 6f;
                dayFont = Ui.F(fontSize, true, false, true);
            }
            var numSize = Ui.Measure(g, dayText, dayFont);
            float availTop = hero.Y + Ui.SF(74);
            float availBottom = hero.Bottom - Ui.SF(135);
            float blockH = Ui.SF(256);
            float blockTop = availTop + Math.Max(0f, (availBottom - availTop - blockH) / 2f);
            using (var unitFont = Ui.FClone(20))
            {
                var unitSize = Ui.Measure(g, L10n.DayWord(days), unitFont);
                float gap = Ui.SF(2);
                float groupW = numSize.Width + gap + unitSize.Width;
                float x = hero.X + (hero.Width - groupW) / 2f;
                float top = blockTop;
                float lineH = dayFont.GetHeight(g);
                var numRect = new RectangleF(x, top, numSize.Width + Ui.SF(2), lineH + Ui.SF(4));
                var unitRect = new RectangleF(x + numSize.Width + gap, top - Ui.SF(14),
                    unitSize.Width + Ui.SF(4), lineH + Ui.SF(4));

                if (!Ui.IsLight)
                {
                    Ui.Text(g, dayText, dayFont, Ui.Shadow(85),
                        new RectangleF(numRect.X, numRect.Y + Ui.SF(5), numRect.Width, numRect.Height),
                        StringAlignment.Near, StringAlignment.Center);
                }
                Color gradTop = Ui.IsLight ? Ui.Mix(Ui.AccentInk(accent), Color.White, 0.18f) : Ui.Lighten(accent, 0.42f);
                Color gradBottom = Ui.IsLight ? Ui.AccentInk(accent) : accent;
                Ui.TextGradient(g, dayText, dayFont, numRect, gradTop, gradBottom,
                    StringAlignment.Near, StringAlignment.Center);
                Ui.Text(g, L10n.DayWord(days), unitFont, Ui.Text2, unitRect, StringAlignment.Near, StringAlignment.Far);
            }

            float pillW = Ui.SF(136), pillH = Ui.SF(76), pillGap = Ui.SF(14);
            float totalW = pillW * 3 + pillGap * 2;
            float px = hero.X + (hero.Width - totalW) / 2f;
            float py = blockTop + Ui.SF(180);
            int[] values = { hours, minutes, seconds };
            string[] labels = { L10n.T("时", "HRS"), L10n.T("分", "MIN"), L10n.T("秒", "SEC") };
            for (int i = 0; i < 3; i++)
            {
                var pr = new RectangleF(px + i * (pillW + pillGap), py, pillW, pillH);
                Ui.FillRound(g, pr, Ui.SF(18), Ui.Overlay(7));
                Ui.StrokeRound(g, pr, Ui.SF(18), Ui.Overlay(12), 1f);
                using (var nf = Ui.FClone(31, true))
                    Ui.Text(g, values[i].ToString("00", CultureInfo.InvariantCulture), nf,
                        Ui.Ink, new RectangleF(pr.X, pr.Y + Ui.SF(10), pr.Width, Ui.SF(36)),
                        StringAlignment.Center, StringAlignment.Center);
                using (var lf = Ui.FClone(9.5f))
                    Ui.Text(g, labels[i], lf, Ui.Text3,
                        new RectangleF(pr.X, pr.Y + Ui.SF(48), pr.Width, Ui.SF(16)),
                        StringAlignment.Center, StringAlignment.Center);
            }

            // timeline: created -> target, or previous anniversary -> next anniversary
            DateTime progressStart;
            string leftLabel, rightLabel, caption;
            double frac;
            if (annualCycle)
            {
                progressStart = EventUtil.ProgressStart(ev, now);
                double totalSeconds = (target - progressStart).TotalSeconds;
                double done = (now - progressStart).TotalSeconds;
                frac = totalSeconds > 0 ? done / totalSeconds : 0;
                caption = L10n.T("年度进度", "Year progress");
                leftLabel = L10n.T("上次 · ", "Last · ") + EventUtil.ShortDay(progressStart);
                rightLabel = L10n.T("下次 · ", "Next · ") + EventUtil.ShortDay(target);
            }
            else
            {
                progressStart = EventUtil.CreatedOf(ev);
                if (future)
                {
                    double totalSeconds = (target - progressStart).TotalSeconds;
                    double done = (now - progressStart).TotalSeconds;
                    frac = totalSeconds > 0 ? done / totalSeconds : 0;
                }
                else frac = 1.0;
                caption = L10n.T("时间进度", "Time progress");
                leftLabel = L10n.T("创建 · ", "Created · ") + EventUtil.ShortDay(progressStart);
                rightLabel = repeatAnnual
                    ? L10n.T("下次 · ", "Next · ") + EventUtil.ShortDay(target)
                    : L10n.T("目标 · ", "Target · ") + EventUtil.ShortDay(target);
            }
            if (frac < 0) frac = 0; else if (frac > 1) frac = 1;

            float lx = hero.X + Ui.SF(46), rx = hero.Right - Ui.SF(46);
            float labelY = hero.Bottom - Ui.SF(115);
            using (var lf = Ui.FClone(9.5f))
                Ui.Text(g, caption, lf, Ui.Text3,
                    new RectangleF(lx, labelY, Ui.SF(200), Ui.SF(16)));
            using (var pf = Ui.FClone(11))
            {
                string ptext = ((int)Math.Round(frac * 100)) + "%";
                Ui.Text(g, ptext, pf, Ui.AccentInk(accent),
                    new RectangleF(rx - Ui.SF(200), labelY - Ui.SF(2), Ui.SF(200), Ui.SF(18)),
                    StringAlignment.Far, StringAlignment.Center);
            }

            float trackY = hero.Bottom - Ui.SF(91);
            float trackW = rx - lx;
            Ui.FillRound(g, new RectangleF(lx, trackY, trackW, Ui.SF(6)), Ui.SF(3), Ui.Overlay(12));
            float fillW = (float)(trackW * frac);
            if (fillW < Ui.SF(6)) fillW = Ui.SF(6);
            using (var br = new LinearGradientBrush(new RectangleF(lx, trackY, Math.Max(1, fillW), Ui.SF(6)),
                Ui.Lighten(accent, 0.25f), accent, 0f))
                Ui.FillRound(g, new RectangleF(lx, trackY, fillW, Ui.SF(6)), Ui.SF(3), br);
            float knobX = lx + fillW;
            Ui.Glow(g, new RectangleF(knobX - Ui.SF(18), trackY - Ui.SF(15) + Ui.SF(3), Ui.SF(36), Ui.SF(36)),
                accent, Ui.GlowAlpha(60));
            Ui.FillRound(g, new RectangleF(knobX - Ui.SF(5), trackY - Ui.SF(2), Ui.SF(10), Ui.SF(10)),
                Ui.SF(5), accent);
            Ui.FillRound(g, new RectangleF(knobX - Ui.SF(2), trackY + Ui.SF(1), Ui.SF(4), Ui.SF(4)),
                Ui.SF(2), Ui.A(Color.White, 220));

            using (var df = Ui.FClone(9))
            {
                Ui.Text(g, leftLabel, df, Ui.Text3, new RectangleF(lx, trackY + Ui.SF(15), trackW / 2f, Ui.SF(16)));
                Ui.Text(g, rightLabel, df, Ui.Text3,
                    new RectangleF(lx + trackW / 2f, trackY + Ui.SF(15), trackW / 2f, Ui.SF(16)),
                    StringAlignment.Far, StringAlignment.Center);
            }
        }

        void DrawMainFooter(Graphics g)
        {
            string status = _store.Dirty ? L10n.T("已自动保存 · 退出时写回 EXE", "Saved · writes to EXE on exit") : L10n.T("配置已内嵌 EXE · 自动保存", "Stored inside EXE · autosaved");
            using (var f = Ui.FClone(9))
            {
                var dot = new RectangleF(Ui.SF(348), H - Ui.SF(39), Ui.SF(5), Ui.SF(5));
                Ui.FillRound(g, dot, Ui.SF(2.5f), _store.Dirty ? Ui.Warn : Ui.Success);
                Ui.Text(g, status, f, Ui.Text3, new RectangleF(Ui.SF(360), H - Ui.SF(48), Ui.SF(380), Ui.SF(20)));
                Ui.Text(g, L10n.T("v0.2 · 时光倒数", "v0.2 · Shiguang"), f, Ui.Text3,
                    new RectangleF(W - Ui.SF(42) - Ui.SF(258), H - Ui.SF(48), Ui.SF(258), Ui.SF(20)),
                    StringAlignment.Far);
            }
        }

        void DrawEmpty(Graphics g)
        {
            float cx = Ui.SF(SidebarW) + (W - Ui.SF(SidebarW)) / 2f;
            float cy = H * 0.38f;
            Ui.Glow(g, new RectangleF(cx - Ui.SF(190), cy - Ui.SF(120), Ui.SF(380), Ui.SF(380)),
                Ui.Brand, Ui.GlowAlpha(15));

            var iconRect = new RectangleF(cx - Ui.SF(38), cy - Ui.SF(38), Ui.SF(76), Ui.SF(76));
            Ui.StrokeRound(g, new RectangleF(iconRect.X - Ui.SF(18), iconRect.Y - Ui.SF(18),
                iconRect.Width + Ui.SF(36), iconRect.Height + Ui.SF(36)), Ui.SF(22), Ui.Overlay(9), 1f);
            Ui.IconCalendar(g, iconRect, Ui.AccentInk(Ui.Brand), Ui.SF(2.2f));

            using (var tf = Ui.FClone(23, false, true))
                Ui.Text(g, L10n.T("把重要的日子，留在这里", "Keep the days that matter"), tf, Ui.Ink,
                    new RectangleF(cx - Ui.SF(300), cy + Ui.SF(78), Ui.SF(600), Ui.SF(38)),
                    StringAlignment.Center, StringAlignment.Center);
            using (var sf = Ui.FClone(11.5f))
                Ui.Text(g, L10n.T("设置一个日期与时间，剩下的交给时间。", "Pick a date and time; let time do the rest."), sf, Ui.Text2,
                    new RectangleF(cx - Ui.SF(300), cy + Ui.SF(120), Ui.SF(600), Ui.SF(24)),
                    StringAlignment.Center, StringAlignment.Center);

            var cta = new Rectangle(Convert.ToInt32(cx - Ui.SF(96)), Convert.ToInt32(H - Ui.SF(250)),
                Ui.S(192), Ui.S(46));
            _emptyCta = cta;
            bool hov = _hover == HoverKind.EmptyCta;
            using (var br = new LinearGradientBrush(cta, hov ? Ui.Lighten(Ui.Brand, 0.1f) : Ui.Brand,
                hov ? Ui.Lighten(Ui.Brand2, 0.1f) : Ui.Brand2, 0f))
                Ui.FillRound(g, cta, Ui.SF(15), br);
            Ui.IconPlus(g, new RectangleF(cta.X + Ui.SF(26), cta.Y + Ui.SF(15), Ui.SF(16), Ui.SF(16)),
                Ui.OnAccent, Ui.SF(1.9f));
            using (var f = Ui.FClone(13, false, true))
                Ui.Text(g, L10n.T("新建第一个日子", "Create your first day"), f, Ui.OnAccent,
                    new RectangleF(cta.X + Ui.SF(52), cta.Y, cta.Width - Ui.SF(58), cta.Height));
        }

        void DrawMini(Graphics g, DateTime now)
        {
            var full = ClientRectangle;
            var accent = _selected == null ? Ui.Brand : EventUtil.AccentOf(_selected, 0);
            var accentInk = Ui.AccentInk(accent);
            using (var br = new LinearGradientBrush(full, Ui.BgTop, Ui.BgBottom, 120f))
                g.FillRectangle(br, full);
            Ui.Glow(g, new RectangleF(W * 0.55f, -H * 0.5f, W * 0.9f, W * 0.9f), accent, Ui.GlowAlpha(28));
            using (var path = Ui.Round(new RectangleF(0, 0, W, H), Ui.SF(20)))
            using (var pen = new Pen(Ui.Overlay(14), 1f))
                g.DrawPath(pen, path);

            DrawWindowButtons(g);

            if (_selected == null)
            {
                using (var f = Ui.FClone(15, false, true))
                    Ui.Text(g, L10n.T("还没有日子", "No days yet"), f, Ui.Ink, new RectangleF(Ui.SF(24), Ui.SF(22), W - Ui.SF(150), Ui.SF(26)));
                using (var f = Ui.FClone(10))
                    Ui.Text(g, L10n.T("展开窗口，创建第一个日子", "Expand the window to create one"), f, Ui.Text2,
                        new RectangleF(Ui.SF(24), Ui.SF(54), W - Ui.SF(48), Ui.SF(20)));
                return;
            }

            var target = EventUtil.EffectiveTarget(_selected, now);
            var original = EventUtil.TargetOf(_selected);
            bool future = target > now;
            var span = future ? target - now : now - target;
            long totalSec = future ? (long)Math.Ceiling(span.TotalSeconds) : (long)Math.Floor(span.TotalSeconds);
            if (totalSec < 0) totalSec = 0;
            int days = (int)(totalSec / 86400);
            int rem = (int)(totalSec % 86400);
            string dayText = days.ToString(CultureInfo.InvariantCulture);

            using (var f = Ui.FClone(15, false, true))
                Ui.Text(g, string.IsNullOrEmpty(_selected.Title) ? L10n.T("未命名日子", "Untitled day") : _selected.Title, f, Ui.Ink,
                    new RectangleF(Ui.SF(24), Ui.SF(16), W - Ui.SF(126), Ui.SF(24)),
                    StringAlignment.Near, StringAlignment.Center, true);
            using (var f = Ui.FClone(9.5f))
            {
                string sub = EventUtil.ShortDate(target) + (future ? L10n.T(" · 还有", " · left") : L10n.T(" · 已过去", " · ago"));
                if (_selected.RepeatYearly) sub += L10n.T(" · 每年", " · yearly");
                Ui.Text(g, sub, f, Ui.Text2, new RectangleF(Ui.SF(24), Ui.SF(42), W - Ui.SF(48), Ui.SF(18)),
                    StringAlignment.Near, StringAlignment.Center, true);
            }

            float fontSize = 74f;
            Font df = Ui.F(fontSize, true, false, true);
            while (fontSize > 40f && Ui.Measure(g, dayText, df).Width > W * 0.42f)
            {
                fontSize -= 4f;
                df = Ui.F(fontSize, true, false, true);
            }
            var nsz = Ui.Measure(g, dayText, df);
            using (var unitFont = Ui.FClone(14))
            {
                var usz = Ui.Measure(g, L10n.DayWord(days), unitFont);
                float x = Ui.SF(24);
                float top = Ui.SF(66);
                float lineH = df.GetHeight(g);
                Color gt = Ui.IsLight ? Ui.Mix(accentInk, Color.White, 0.18f) : Ui.Lighten(accent, 0.42f);
                Color gb = Ui.IsLight ? accentInk : accent;
                Ui.TextGradient(g, dayText, df, new RectangleF(x, top, nsz.Width + 2, lineH + 4), gt, gb,
                    StringAlignment.Near, StringAlignment.Center);
                Ui.Text(g, L10n.DayWord(days), unitFont, Ui.Text2,
                    new RectangleF(x + nsz.Width + Ui.SF(2), top - Ui.SF(8), usz.Width + 6, lineH + 4),
                    StringAlignment.Near, StringAlignment.Far);
            }

            using (var f = Ui.FClone(24, true))
                Ui.Text(g, string.Format("{0:00}:{1:00}:{2:00}", rem / 3600, (rem % 3600) / 60, rem % 60),
                    f, Ui.Ink, new RectangleF(Ui.SF(24), Ui.SF(154), W - Ui.SF(48), Ui.SF(32)),
                    StringAlignment.Near, StringAlignment.Center);
            using (var f = Ui.FClone(9))
                Ui.Text(g, _selected.RepeatYearly ? L10n.T("每年重复", "Yearly repeat") : L10n.T("仅此一次", "One-off"), f, Ui.Text3,
                    new RectangleF(W - Ui.SF(130), Ui.SF(162), Ui.SF(106), Ui.SF(18)),
                    StringAlignment.Far, StringAlignment.Center);

            float trackY = H - Ui.SF(18);
            Ui.FillRound(g, new RectangleF(Ui.SF(24), trackY, W - Ui.SF(48), Ui.SF(4)), Ui.SF(2), Ui.Overlay(12));
            double frac = 0;
            if (future)
            {
                var start = _selected.RepeatYearly && _selected.Mode != 2
                    ? EventUtil.ProgressStart(_selected, now)
                    : EventUtil.CreatedOf(_selected);
                double total = (target - start).TotalSeconds;
                double done = (now - start).TotalSeconds;
                frac = total > 0 ? done / total : 0;
            }
            else frac = 1;
            if (frac < 0) frac = 0; else if (frac > 1) frac = 1;
            float fill = Math.Max(Ui.SF(4), (float)((W - Ui.SF(48)) * frac));
            using (var br = new LinearGradientBrush(new RectangleF(Ui.SF(24), trackY, Math.Max(1, fill), Ui.SF(4)),
                Ui.Lighten(accent, 0.25f), accent, 0f))
                Ui.FillRound(g, new RectangleF(Ui.SF(24), trackY, fill, Ui.SF(4)), Ui.SF(2), br);
        }

        void DrawTooltip(Graphics g)
        {
            if (_tip == null || _hover == HoverKind.None || _hover == HoverKind.Header || _hover == HoverKind.Card)
                return;
            if ((DateTime.Now - _hoverAt).TotalMilliseconds < 520) return;
            _tipPainted = true;
            using (var f = Ui.FClone(10))
            {
                var sz = Ui.Measure(g, _tip, f);
                float w = Math.Min(sz.Width + Ui.SF(20), W - Ui.SF(16));
                float h = Ui.SF(26);
                float x = Math.Min(_mouse.X + Ui.SF(14), W - w - Ui.SF(8));
                float y = _mouse.Y + Ui.SF(22);
                if (x < Ui.SF(8)) x = Ui.SF(8);
                if (y + h > H - Ui.SF(8)) y = _mouse.Y - h - Ui.SF(12);
                if (y < Ui.SF(8)) y = Ui.SF(8);
                var r = new RectangleF(x, y, w, h);
                Ui.FillRound(g, new RectangleF(r.X + Ui.SF(2), r.Y + Ui.SF(3), r.Width, r.Height),
                    Ui.SF(8), Ui.Shadow(Ui.IsLight ? 30 : 70));
                Ui.FillRound(g, r, Ui.SF(8), Ui.TooltipBg);
                Ui.StrokeRound(g, r, Ui.SF(8), Ui.TooltipBorder, 1f);
                Ui.Text(g, _tip, f, Ui.TooltipText,
                    new RectangleF(r.X, r.Y - Ui.SF(1), r.Width, r.Height),
                    StringAlignment.Center, StringAlignment.Center, true);
            }
        }
    }
}
