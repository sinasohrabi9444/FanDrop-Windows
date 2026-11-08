using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;

namespace FanDrop
{
    // Watches global mouse input for a left-button drag. While one is in progress, pressing
    // Shift / Ctrl / Alt pops the fan open under the cursor. Keys already held when the drag
    // started (e.g. Ctrl/Shift multi-select in Explorer) are ignored until pressed again.
    //
    // The low-level hook lives on its own thread: Windows silently removes an LL hook whose
    // callback stalls, and the UI thread can be busy (e.g. while the overlay first renders).
    class DragWatcher
    {
        static readonly int[] ModifierKeys = { Native.VK_SHIFT, Native.VK_CONTROL, Native.VK_MENU };

        readonly FanWindow fan;
        readonly Dispatcher ui;
        readonly DispatcherTimer timer;
        readonly bool[] prev = new bool[3];
        Native.HookProc proc;
        uint hookThreadId;

        // hook-thread state
        bool down, dragging;
        Native.POINT downPt;

        public DragWatcher(FanWindow fan)
        {
            this.fan = fan;
            ui = fan.Dispatcher;
            timer = new DispatcherTimer(DispatcherPriority.Input, ui);
            timer.Interval = TimeSpan.FromMilliseconds(25);
            timer.Tick += Tick;
        }

        public void Start()
        {
            var t = new Thread(HookThread) { IsBackground = true, Name = "FanDrop mouse hook" };
            t.Start();
        }

        public void Stop()
        {
            if (hookThreadId != 0) Native.PostThreadMessage(hookThreadId, Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            timer.Stop();
        }

        void HookThread()
        {
            hookThreadId = Native.GetCurrentThreadId();
            proc = HookCallback;
            IntPtr hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, proc, Native.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero)
            {
                Log.W("mouse hook failed: " + Marshal.GetLastWin32Error());
                return;
            }
            Log.W("mouse hook installed");
            Native.MSG msg;
            while (Native.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                Native.TranslateMessage(ref msg);
                Native.DispatchMessage(ref msg);
            }
            Native.UnhookWindowsHookEx(hook);
        }

        IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == Native.WM_LBUTTONDOWN)
                {
                    var s = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                    down = true;
                    dragging = false;
                    downPt = s.pt;
                    Log.D("button down at " + s.pt.X + "," + s.pt.Y + " flags " + s.flags);
                }
                else if (msg == Native.WM_MOUSEMOVE && down && !dragging)
                {
                    var s = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                    if (Math.Abs(s.pt.X - downPt.X) > 6 || Math.Abs(s.pt.Y - downPt.Y) > 6)
                    {
                        dragging = true;
                        ui.BeginInvoke(new Action(DragStarted));
                    }
                }
                else if (msg == Native.WM_LBUTTONUP)
                {
                    down = false;
                    if (dragging)
                    {
                        dragging = false;
                        ui.BeginInvoke(new Action(DragEnded));
                    }
                }
            }
            return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        // ---- UI thread ----

        void DragStarted()
        {
            for (int i = 0; i < 3; i++) prev[i] = Native.KeyDown(ModifierKeys[i]);
            Log.D("drag started; held: " + prev[0] + "/" + prev[1] + "/" + prev[2]);
            timer.Start();
        }

        void DragEnded()
        {
            if (!timer.IsEnabled) return;
            Log.D("drag ended");
            timer.Stop();
            fan.DragEnded();
        }

        void Tick(object sender, EventArgs e)
        {
            // safety net in case the button-up never reached the hook
            if (!Native.PrimaryButtonDown())
            {
                DragEnded();
                return;
            }
            for (int i = 0; i < 3; i++)
            {
                bool cur = Native.KeyDown(ModifierKeys[i]);
                if (cur && !prev[i])
                {
                    Log.D("modifier pressed: " + (FanMode)i);
                    fan.ModifierPressed((FanMode)i);
                }
                prev[i] = cur;
            }
        }
    }
}
