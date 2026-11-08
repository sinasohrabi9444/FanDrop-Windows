using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FanDrop
{
    // Stand-in for mac Quick Look: Space / Esc closes, Left / Right steps through the dropped files.
    class QuickLookWindow : Window
    {
        static readonly Brush Bg = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1F));
        static readonly Brush Fg = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));
        static readonly Brush Dim = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0xA0));
        static readonly HashSet<string> TextLike = new HashSet<string>(
            "txt md log csv tsv json xml yml yaml ini cfg conf toml cs js ts tsx jsx py rb go rs java kt swift c h cpp hpp css scss html htm sql sh bat ps1 srt vtt".Split(' '));

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        readonly List<string> files;
        readonly TextBlock title = new TextBlock();
        readonly ContentControl body = new ContentControl();
        int index;
        MediaElement media;

        public QuickLookWindow(List<string> files)
        {
            this.files = files;
            var wa = SystemParameters.WorkArea;
            Width = Math.Min(1100, wa.Width * 0.7);
            Height = Math.Min(800, wa.Height * 0.75);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Bg;
            Foreground = Fg;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            Topmost = true;

            var open = new Button { Content = "Open", Padding = new Thickness(14, 3, 14, 3), Margin = new Thickness(8, 0, 0, 0) };
            open.Click += delegate { Host.Shell(files[index]); Close(); };
            title.Foreground = Fg;
            title.FontSize = 14;
            title.FontWeight = FontWeights.SemiBold;
            title.VerticalAlignment = VerticalAlignment.Center;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            var bar = new DockPanel { Margin = new Thickness(14, 10, 14, 10) };
            DockPanel.SetDock(open, Dock.Right);
            bar.Children.Add(open);
            bar.Children.Add(title);

            var dock = new DockPanel();
            DockPanel.SetDock(bar, Dock.Top);
            dock.Children.Add(bar);
            dock.Children.Add(body);
            Content = dock;

            SourceInitialized += delegate
            {
                int on = 1; // DWMWA_USE_IMMERSIVE_DARK_MODE
                DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(this).Handle, 20, ref on, 4);
            };
            PreviewKeyDown += OnKey;
            Closed += delegate { StopMedia(); };
            Loaded += delegate { Activate(); ShowItem(0); };
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || (e.Key == Key.Space && media == null)) { Close(); e.Handled = true; }
            else if (e.Key == Key.Space && media != null) { TogglePlay(); e.Handled = true; }
            else if (e.Key == Key.Right && files.Count > 1) { ShowItem((index + 1) % files.Count); e.Handled = true; }
            else if (e.Key == Key.Left && files.Count > 1) { ShowItem((index + files.Count - 1) % files.Count); e.Handled = true; }
        }

        void ShowItem(int i)
        {
            StopMedia();
            index = i;
            string f = files[i];
            string name = Path.GetFileName(f.TrimEnd('\\'));
            Title = name + " — FanDrop";
            title.Text = files.Count > 1 ? name + "   (" + (i + 1) + "/" + files.Count + ")" : name;
            try
            {
                switch (Kinds.Of(f))
                {
                    case FileKind.Image: ShowImage(Images.Load(f)); break;
                    case FileKind.Video: ShowMedia(f, true); break;
                    case FileKind.Audio: ShowMedia(f, false); break;
                    case FileKind.Pdf: ShowPdf(f); break;
                    case FileKind.Document: ShowText(Docs.ReadText(f), false); break;
                    default:
                        if (File.Exists(f) && TextLike.Contains(Kinds.Ext(f))) ShowText(ReadHead(f), true);
                        else ShowInfo(f);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.W("quick look failed: " + ex.Message);
                ShowInfo(f);
            }
        }

        void ShowImage(BitmapSource img)
        {
            body.Content = new Image { Source = img, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(12) };
        }

        void ShowText(string text, bool mono)
        {
            body.Content = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.Transparent,
                Foreground = Fg,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(18, 6, 18, 18),
                FontSize = 13.5,
                FontFamily = mono ? new FontFamily("Cascadia Mono, Consolas") : FontFamily,
                FlowDirection = FlowDirection.LeftToRight
            };
        }

        void ShowMedia(string f, bool video)
        {
            media = new MediaElement { Source = new Uri(f), LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close, Stretch = Stretch.Uniform };
            media.MediaFailed += (s, e) => { Log.W("media failed: " + e.ErrorException.Message); StopMedia(); ShowInfo(f); };
            var grid = new Grid { Margin = new Thickness(12) };
            if (!video)
                grid.Children.Add(new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 120, Foreground = Dim, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            grid.Children.Add(media);
            grid.Children.Add(new TextBlock { Text = "Space: play / pause", Foreground = Dim, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom });
            grid.MouseLeftButtonUp += delegate { TogglePlay(); };
            body.Content = grid;
            media.Play();
            media.Tag = "playing";
        }

        void TogglePlay()
        {
            if (media == null) return;
            if ((string)media.Tag == "playing") { media.Pause(); media.Tag = "paused"; }
            else { media.Play(); media.Tag = "playing"; }
        }

        void StopMedia()
        {
            if (media != null) { media.Stop(); media.Close(); media = null; }
        }

        void ShowPdf(string f)
        {
            var stack = new StackPanel { Margin = new Thickness(12) };
            body.Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            stack.Children.Add(new TextBlock { Text = "Rendering…", Foreground = Dim, HorizontalAlignment = HorizontalAlignment.Center });
            int shown = index;
            var t = new Thread(() =>
            {
                try
                {
                    var pages = WinRtPdf.Render(f, 30);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (shown != index) return;
                        stack.Children.Clear();
                        foreach (var png in pages)
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.StreamSource = new MemoryStream(png);
                            bmp.EndInit();
                            stack.Children.Add(new Image { Source = bmp, Stretch = Stretch.Uniform, MaxWidth = Width - 60, Margin = new Thickness(0, 0, 0, 12) });
                        }
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() => { if (shown == index) ShowInfo(f); }));
                    Log.W("pdf preview failed: " + ex.Message);
                }
            }) { IsBackground = true };
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();
        }

        void ShowInfo(string f)
        {
            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            bool dir = Directory.Exists(f);
            panel.Children.Add(new TextBlock { Text = dir ? "" : "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 96, Foreground = Dim, HorizontalAlignment = HorizontalAlignment.Center });
            panel.Children.Add(new TextBlock { Text = Path.GetFileName(f.TrimEnd('\\')), FontSize = 18, Foreground = Fg, Margin = new Thickness(0, 14, 0, 4), HorizontalAlignment = HorizontalAlignment.Center });
            string detail;
            if (dir)
            {
                int n = 0;
                try { n = Directory.EnumerateFileSystemEntries(f).Count(); } catch { }
                detail = "Folder · " + n + " items";
            }
            else
            {
                var fi = new FileInfo(f);
                detail = Size(fi.Length) + " · modified " + fi.LastWriteTime.ToString("g");
            }
            panel.Children.Add(new TextBlock { Text = detail, Foreground = Dim, HorizontalAlignment = HorizontalAlignment.Center });
            body.Content = panel;
        }

        static string ReadHead(string f)
        {
            using (var r = new StreamReader(f, System.Text.Encoding.UTF8, true))
            {
                var buf = new char[200000];
                int n = r.ReadBlock(buf, 0, buf.Length);
                return new string(buf, 0, n) + (r.EndOfStream ? "" : "\n…");
            }
        }

        static string Size(long b)
        {
            string[] u = { "bytes", "KB", "MB", "GB", "TB" };
            double v = b;
            int i = 0;
            while (v >= 1000 && i < u.Length - 1) { v /= 1000; i++; }
            return i == 0 ? b + " bytes" : v.ToString("0.#") + " " + u[i];
        }
    }
}
