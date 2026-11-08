using System;
using System.Collections.Generic;

using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FanDrop
{
    // Transparent, non-activating, topmost window centred on the cursor. It first appears as an
    // invisible "probe" (alpha 1/255, so it still receives OLE drag events); only when a drag
    // carrying files enters it does the fan of wedges animate in.
    class FanWindow : Window
    {
        const double Outer = 176, Inner = 64, Pad = 28, ChipsH = 44, GapPx = 3;
        static readonly FontFamily IconFont = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        static readonly FontFamily UiFont = new FontFamily("Segoe UI Variable Text, Segoe UI");
        static readonly Brush WedgeFill = Frozen(Color.FromArgb(0xEE, 0x1E, 0x1E, 0x22));
        static readonly Brush WedgeStroke = Frozen(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
        static readonly Brush TextMain = Frozen(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
        static readonly Brush TextDim = Frozen(Color.FromArgb(0xA0, 0xFF, 0xFF, 0xFF));

        readonly double cx, cy;
        readonly Grid root = new Grid();
        readonly Canvas ring = new Canvas();
        readonly ScaleTransform zoom = new ScaleTransform(1, 1);
        readonly TextBlock centerTitle = new TextBlock();
        readonly TextBlock centerSub = new TextBlock();
        readonly Ellipse centerDisc = new Ellipse();
        readonly StackPanel chipRow = new StackPanel();
        readonly DispatcherTimer hideTimer = new DispatcherTimer();
        readonly List<Path> wedges = new List<Path>();
        readonly List<FrameworkElement> wedgeLabels = new List<FrameworkElement>();
        readonly List<TranslateTransform> wedgeShift = new List<TranslateTransform>();

        FanMode mode;
        FileKind kind;
        List<string> names = new List<string>();
        string[] slots = new string[0];
        int hot = -1;
        bool revealed, rejected;
        int generation;

        static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public FanWindow()
        {
            double w = 2 * (Outer + Pad);
            cx = w / 2;
            cy = Outer + Pad;
            Width = w;
            Height = w + ChipsH;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            AllowDrop = true;
            Focusable = false;
            Title = "FanDrop";
            FontFamily = UiFont;

            SourceInitialized += delegate
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
                Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
            };

            hideTimer.Interval = TimeSpan.FromMilliseconds(450);
            hideTimer.Tick += delegate { hideTimer.Stop(); HideNow(); };

            BuildChrome();
            Content = root;

            DragEnter += OnDragEnter;
            DragOver += OnDragOver;
            DragLeave += OnDragLeave;
            Drop += OnDrop;
        }

        void BuildChrome()
        {
            root.RenderTransform = zoom;
            root.RenderTransformOrigin = new Point(cx / Width, cy / Height);
            root.Opacity = 0;

            ring.Width = Width;
            ring.Height = Height;
            ring.Effect = new DropShadowEffect { BlurRadius = 28, ShadowDepth = 6, Opacity = 0.45, Color = Colors.Black, Direction = 270 };
            root.Children.Add(ring);

            double d = 2 * (Inner - GapPx * 2);
            centerDisc.Width = d;
            centerDisc.Height = d;
            centerDisc.Fill = Frozen(Color.FromArgb(0xF4, 0x16, 0x16, 0x19));
            centerDisc.Stroke = WedgeStroke;
            centerDisc.StrokeThickness = 1;
            centerDisc.HorizontalAlignment = HorizontalAlignment.Left;
            centerDisc.VerticalAlignment = VerticalAlignment.Top;
            centerDisc.Margin = new Thickness(cx - d / 2, cy - d / 2, 0, 0);
            root.Children.Add(centerDisc);

            var text = new StackPanel { Width = d - 18, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            text.Margin = new Thickness(cx - (d - 18) / 2, 0, 0, Height - 2 * cy);
            centerTitle.Foreground = TextMain;
            centerTitle.FontSize = 12.5;
            centerTitle.FontWeight = FontWeights.SemiBold;
            centerTitle.TextAlignment = TextAlignment.Center;
            centerTitle.TextWrapping = TextWrapping.Wrap;
            centerTitle.TextTrimming = TextTrimming.CharacterEllipsis;
            centerTitle.MaxHeight = 34;
            centerSub.Foreground = TextDim;
            centerSub.FontSize = 10.5;
            centerSub.TextAlignment = TextAlignment.Center;
            centerSub.TextTrimming = TextTrimming.CharacterEllipsis;
            centerSub.Margin = new Thickness(0, 2, 0, 0);
            text.Children.Add(centerTitle);
            text.Children.Add(centerSub);
            root.Children.Add(text);

            chipRow.Orientation = Orientation.Horizontal;
            chipRow.HorizontalAlignment = HorizontalAlignment.Center;
            chipRow.VerticalAlignment = VerticalAlignment.Top;
            chipRow.Margin = new Thickness(0, cy + Outer + 14, 0, 0);
            root.Children.Add(chipRow);
        }

        // renders the fan as it looks mid-drag (developer hook, used to check the visuals)
        public static void Snapshot(string png, FanMode mode, string file, int hotSlot)
        {
            var w = new FanWindow();
            w.mode = mode;
            w.names = new List<string> { file };
            w.kind = Kinds.Of(file);
            w.Rebuild();
            w.SetHot(hotSlot);
            w.root.Opacity = 1;
            w.root.Measure(new Size(w.Width, w.Height));
            w.root.Arrange(new Rect(0, 0, w.Width, w.Height));
            w.root.UpdateLayout();
            var bg = new DrawingVisual();
            using (var dc = bg.RenderOpen())
            {
                dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x6A, 0x8C, 0xAF), Color.FromRgb(0xD8, 0xC8, 0xB0), 45), null, new Rect(0, 0, w.Width, w.Height));
            }
            var rtb = new RenderTargetBitmap((int)(w.Width * 2), (int)(w.Height * 2), 192, 192, PixelFormats.Pbgra32);
            rtb.Render(bg);
            rtb.Render(w.root);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = System.IO.File.Create(png)) enc.Save(fs);
        }

        // ---- called by DragWatcher ----

        public void ModifierPressed(FanMode m)
        {
            if (rejected) return;
            mode = m;
            if (!IsVisible)
            {
                generation++;
                hideTimer.Stop();
                revealed = false;
                hot = -1;
                root.BeginAnimation(OpacityProperty, null);
                root.Opacity = 0;
                PositionAtCursor();
                Show();
                Log.D("probe shown at " + Left + "," + Top);
            }
            else if (revealed)
            {
                hot = -1;
                Rebuild();
            }
        }

        public void DragEnded()
        {
            rejected = false;
            if (IsVisible)
            {
                hideTimer.Stop();
                hideTimer.Start();
            }
        }

        void PositionAtCursor()
        {
            Native.POINT p;
            Native.GetCursorPos(out p);
            double s = Native.DpiScale();
            var wa = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(p.X, p.Y)).WorkingArea;
            double left = p.X / s - cx, top = p.Y / s - cy;
            left = Math.Max(wa.Left / s - Pad, Math.Min(left, wa.Right / s - Width + Pad));
            top = Math.Max(wa.Top / s - Pad, Math.Min(top, wa.Bottom / s - Height + Pad));
            Left = left;
            Top = top;
        }

        // ---- drag & drop ----

        void OnDragEnter(object sender, DragEventArgs e)
        {
            var n = DropData.Names(e.Data);
            if (n.Count == 0)
            {
                Log.W("drag without files; types: " + string.Join(", ", e.Data.GetFormats()));
                rejected = true;
                HideNow();
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }
            names = n;
            kind = Kinds.Of(n[0]);
            if (!revealed)
            {
                Log.W("fan open: " + Slots.ModeName(mode) + " / " + kind + " / " + n.Count + " item(s)");
                Reveal();
            }
            SetHot(HitSlot(e.GetPosition(this)));
            e.Effects = DropEffect(e);
            e.Handled = true;
        }

        void OnDragOver(object sender, DragEventArgs e)
        {
            SetHot(revealed ? HitSlot(e.GetPosition(this)) : -1);
            e.Effects = revealed ? DropEffect(e) : DragDropEffects.None;
            e.Handled = true;
        }

        void OnDragLeave(object sender, DragEventArgs e)
        {
            SetHot(-1);
        }

        void OnDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            hideTimer.Stop();
            int slot = revealed ? HitSlot(e.GetPosition(this)) : -1;
            string label = slot >= 0 ? slots[slot] : null;
            FanMode m = mode;
            e.Effects = slot >= 0 ? DropEffect(e) : DragDropEffects.None;
            FadeOut();
            if (label == null)
            {
                Log.W("drop missed any slot");
                return;
            }
            List<string> files;
            try { files = DropData.Resolve(e.Data); }
            catch (Exception ex) { Log.W("promise failed: " + ex.Message); Host.Notify("FanDrop", "Couldn't read the dropped item: " + ex.Message, null, true); return; }
            if (files.Count == 0)
            {
                Log.W("drop had no files; types: " + string.Join(", ", e.Data.GetFormats()));
                return;
            }
            Actions.Run(label, m, files);
        }

        static DragDropEffects DropEffect(DragEventArgs e)
        {
            // never report Move: Explorer would delete the source after a Shift-drag
            if ((e.AllowedEffects & DragDropEffects.Copy) != 0) return DragDropEffects.Copy;
            if ((e.AllowedEffects & DragDropEffects.Link) != 0) return DragDropEffects.Link;
            return DragDropEffects.None;
        }

        // ---- show / hide ----

        void Reveal()
        {
            revealed = true;
            hot = -1;
            Rebuild();
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
            var grow = new DoubleAnimation(0.7, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease };
            zoom.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            zoom.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
        }

        void FadeOut()
        {
            int g = ++generation;
            var a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(120));
            a.Completed += delegate { if (g == generation) HideNow(); };
            root.BeginAnimation(OpacityProperty, a);
            var shrink = new DoubleAnimation(0.92, TimeSpan.FromMilliseconds(120));
            zoom.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
            zoom.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        }

        void HideNow()
        {
            generation++;
            hideTimer.Stop();
            revealed = false;
            hot = -1;
            root.BeginAnimation(OpacityProperty, null);
            root.Opacity = 0;
            if (IsVisible) Hide();
        }

        // ---- drawing ----

        void Rebuild()
        {
            slots = Slots.For(mode, kind);
            ring.Children.Clear();
            wedges.Clear();
            wedgeLabels.Clear();
            wedgeShift.Clear();
            int n = slots.Length;
            double span = 360.0 / n;
            for (int i = 0; i < n; i++)
            {
                double mid = i * span;
                var shift = new TranslateTransform();
                var path = new Path
                {
                    Data = Sector(mid - span / 2, mid + span / 2, Inner, Outer),
                    Fill = WedgeFill,
                    Stroke = WedgeStroke,
                    StrokeThickness = 1,
                    RenderTransform = shift
                };
                ring.Children.Add(path);

                var label = new StackPanel { Width = Math.Min(104, span / 360 * 2 * Math.PI * (Inner + Outer) / 2 - 8), IsHitTestVisible = false, RenderTransform = shift };
                label.Children.Add(new TextBlock { Text = Slots.Icon(slots[i]), FontFamily = IconFont, FontSize = 21, Foreground = TextMain, HorizontalAlignment = HorizontalAlignment.Center });
                label.Children.Add(new TextBlock
                {
                    Text = slots[i],
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextMain,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 5, 0, 0)
                });
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Point c = Polar((Inner + Outer) / 2 + 4, mid);
                Canvas.SetLeft(label, c.X - label.Width / 2);
                Canvas.SetTop(label, c.Y - label.DesiredSize.Height / 2);
                ring.Children.Add(label);

                wedges.Add(path);
                wedgeLabels.Add(label);
                wedgeShift.Add(shift);
            }
            UpdateChips();
            UpdateCenter();
        }

        void SetHot(int i)
        {
            if (i == hot) return;
            int old = hot;
            hot = i;
            Color accent = Slots.Accent(mode);
            if (old >= 0 && old < wedges.Count) StyleWedge(old, false, accent);
            if (hot >= 0 && hot < wedges.Count) StyleWedge(hot, true, accent);
            UpdateCenter();
        }

        void StyleWedge(int i, bool on, Color accent)
        {
            wedges[i].Fill = on ? new SolidColorBrush(accent) : WedgeFill;
            wedges[i].Stroke = on ? Frozen(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)) : WedgeStroke;
            double mid = i * 360.0 / slots.Length * Math.PI / 180;
            double dist = on ? 7 : 0;
            var dur = TimeSpan.FromMilliseconds(130);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            wedgeShift[i].BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dist * Math.Sin(mid), dur) { EasingFunction = ease });
            wedgeShift[i].BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-dist * Math.Cos(mid), dur) { EasingFunction = ease });
        }

        void UpdateCenter()
        {
            string what = names.Count == 1 ? System.IO.Path.GetFileName(names[0].TrimEnd('\\')) : names.Count + " files";
            if (hot >= 0 && hot < slots.Length)
            {
                centerTitle.Text = slots[hot];
                centerSub.Text = what;
            }
            else
            {
                centerTitle.Text = what;
                centerSub.Text = Slots.ModeName(mode).ToUpperInvariant();
            }
            centerDisc.Stroke = new SolidColorBrush(Color.FromArgb(0x90, Slots.Accent(mode).R, Slots.Accent(mode).G, Slots.Accent(mode).B));
        }

        void UpdateChips()
        {
            chipRow.Children.Clear();
            foreach (FanMode m in new[] { FanMode.Convert, FanMode.Tools, FanMode.Actions })
            {
                bool on = m == mode;
                var t = new TextBlock { FontSize = 11 };
                t.Inlines.Add(new System.Windows.Documents.Run(Slots.ModeKey(m) + "  ") { FontWeight = FontWeights.Bold });
                t.Inlines.Add(new System.Windows.Documents.Run(Slots.ModeName(m)));
                t.Foreground = on ? Brushes.White : TextDim;
                chipRow.Children.Add(new Border
                {
                    Child = t,
                    CornerRadius = new CornerRadius(11),
                    Padding = new Thickness(10, 3, 10, 4),
                    Margin = new Thickness(3, 0, 3, 0),
                    Background = on ? new SolidColorBrush(Slots.Accent(m)) : WedgeFill,
                    BorderBrush = WedgeStroke,
                    BorderThickness = new Thickness(1)
                });
            }
        }

        Point Polar(double r, double deg)
        {
            double a = deg * Math.PI / 180;
            return new Point(cx + r * Math.Sin(a), cy - r * Math.Cos(a));
        }

        // annular sector between two angles (degrees clockwise from 12 o'clock), with an even pixel gap
        Geometry Sector(double a0, double a1, double ri, double ro)
        {
            double go = GapPx / ro * 180 / Math.PI, gi = GapPx / ri * 180 / Math.PI;
            bool large = (a1 - a0) - 2 * go > 180;
            var fig = new PathFigure { StartPoint = Polar(ro, a0 + go), IsClosed = true };
            fig.Segments.Add(new ArcSegment(Polar(ro, a1 - go), new Size(ro, ro), 0, large, SweepDirection.Clockwise, true));
            fig.Segments.Add(new LineSegment(Polar(ri, a1 - gi), true));
            fig.Segments.Add(new ArcSegment(Polar(ri, a0 + gi), new Size(ri, ri), 0, large, SweepDirection.Counterclockwise, true));
            var g = new PathGeometry(new[] { fig });
            g.Freeze();
            return g;
        }

        int HitSlot(Point p)
        {
            if (slots.Length == 0) return -1;
            double dx = p.X - cx, dy = p.Y - cy;
            double r = Math.Sqrt(dx * dx + dy * dy);
            if (r < Inner - 4 || r > Outer + 24) return -1;
            double ang = Math.Atan2(dx, -dy) * 180 / Math.PI;
            if (ang < 0) ang += 360;
            double span = 360.0 / slots.Length;
            return (int)(((ang + span / 2) % 360) / span);
        }
    }
}
