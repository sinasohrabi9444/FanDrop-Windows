using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace FanDrop
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--make-icon")
            {
                IconArt.WriteIco(args[1]);
                return;
            }
            // developer hooks: --run <Convert|Tools|Actions> <slot> <files...>   /   --snapshot <png> <mode> <file>
            if (args.Length >= 4 && args[0] == "--run")
            {
                Actions.RunNow(args[2], (FanMode)Enum.Parse(typeof(FanMode), args[1]), args.Skip(3).ToList());
                return;
            }
            if (args.Length >= 4 && args[0] == "--snapshot")
            {
                FanWindow.Snapshot(args[1], (FanMode)Enum.Parse(typeof(FanMode), args[2]), args[3], args.Length > 4 ? int.Parse(args[4]) : -1);
                return;
            }

            bool first;
            var mutex = new Mutex(true, "FanDrop.SingleInstance.v1", out first);
            if (!first)
            {
                MessageBox.Show("FanDrop is already running — look for the fan icon in the system tray.", "FanDrop");
                return;
            }

            Forms.Application.EnableVisualStyles();
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.DispatcherUnhandledException += (s, e) => { Log.W("UI error: " + e.Exception); e.Handled = true; };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.W("fatal: " + e.ExceptionObject);
            app.Startup += delegate { Host.Start(args.Contains("--autostart")); };
            app.Exit += delegate { Host.Stop(); };
            app.Run();
            GC.KeepAlive(mutex);
        }
    }

    static class Host
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static Dispatcher Ui;
        static Forms.NotifyIcon tray;
        static FanWindow fan;
        static DragWatcher watcher;
        static string revealOnClick;
        static int busy;

        public static string OutputDir
        {
            get { return Environment.GetEnvironmentVariable("FANDROP_OUT") ?? Path.Combine(Native.DownloadsFolder(), "FanDrop"); }
        }

        public static void Start(bool autostart)
        {
            Ui = Dispatcher.CurrentDispatcher;
            Log.W("FanDrop running");

            fan = new FanWindow();
            new System.Windows.Interop.WindowInteropHelper(fan).EnsureHandle(); // keep the first pop-up fast
            watcher = new DragWatcher(fan);
            watcher.Start();

            tray = new Forms.NotifyIcon { Icon = IconArt.TrayIcon(), Text = "FanDrop", Visible = true };
            var menu = new Forms.ContextMenuStrip();
            var header = new Forms.ToolStripMenuItem("FanDrop") { Enabled = false };
            header.Font = new System.Drawing.Font(header.Font, System.Drawing.FontStyle.Bold);
            menu.Items.Add(header);
            menu.Items.Add(new Forms.ToolStripMenuItem("Drag a file, then hold Shift / Ctrl / Alt") { Enabled = false });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Open output folder", null, delegate { OpenOutputFolder(); });
            menu.Items.Add("Show log", null, delegate { Shell(Log.PathName); });
            var auto = new Forms.ToolStripMenuItem("Start with Windows") { Checked = AutostartEnabled(), CheckOnClick = true };
            auto.CheckedChanged += delegate { SetAutostart(auto.Checked); };
            menu.Items.Add(auto);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Quit FanDrop", null, delegate { Application.Current.Shutdown(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { OpenOutputFolder(); };
            tray.BalloonTipClicked += delegate
            {
                if (revealOnClick != null && (File.Exists(revealOnClick) || Directory.Exists(revealOnClick)))
                    Native.Reveal(new[] { revealOnClick });
            };

            if (!autostart)
                Notify("FanDrop is running", "Drag a file, then hold Shift (Convert), Ctrl (Tools) or Alt (Actions).", null, false);
        }

        public static void Stop()
        {
            if (watcher != null) watcher.Stop();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
        }

        public static void Notify(string title, string text, string reveal, bool error)
        {
            Log.W("notify: " + title + " | " + text.Replace("\n", " / "));
            if (Ui == null) return;
            Ui.BeginInvoke(new Action(delegate
            {
                revealOnClick = reveal;
                tray.ShowBalloonTip(5000, title, text.Length > 250 ? text.Substring(0, 250) + "…" : text,
                    error ? Forms.ToolTipIcon.Error : Forms.ToolTipIcon.None);
            }));
        }

        public static void Busy(bool on)
        {
            int n = on ? Interlocked.Increment(ref busy) : Interlocked.Decrement(ref busy);
            if (Ui != null) Ui.BeginInvoke(new Action(delegate { tray.Text = n > 0 ? "FanDrop — working…" : "FanDrop"; }));
        }

        public static void Clipboard(string text)
        {
            if (Ui == null) Ui = Dispatcher.CurrentDispatcher;
            Ui.Invoke(new Action(delegate
            {
                for (int i = 0; i < 10; i++)
                {
                    try { System.Windows.Clipboard.SetDataObject(text, true); return; }
                    catch (System.Runtime.InteropServices.COMException) { Thread.Sleep(50); }
                }
            }));
        }

        public static void OnUi(Action a)
        {
            Ui.BeginInvoke(a);
        }

        public static void Shell(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { Notify("FanDrop", "Couldn't open " + Path.GetFileName(path) + ": " + ex.Message, null, true); }
        }

        static void OpenOutputFolder()
        {
            Directory.CreateDirectory(OutputDir);
            Shell(OutputDir);
        }

        static bool AutostartEnabled()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue("FanDrop") != null;
        }

        static void SetAutostart(bool on)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue("FanDrop", "\"" + Forms.Application.ExecutablePath + "\" --autostart");
                else k.DeleteValue("FanDrop", false);
            }
        }
    }

    static class Log
    {
        static readonly object gate = new object();
        public static readonly string PathName = Path.Combine(Path.GetTempPath(), "fandrop.log");

        static readonly bool debug = Environment.GetEnvironmentVariable("FANDROP_DEBUG") != null;

        // verbose tracing, only with FANDROP_DEBUG set
        public static void D(string s)
        {
            if (debug) W("[debug] " + s);
        }

        public static void W(string s)
        {
            lock (gate)
            {
                try { File.AppendAllText(PathName, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + s + Environment.NewLine); }
                catch { }
            }
        }
    }
}
