using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace FanDrop
{
    static class Native
    {
        public const int WH_MOUSE_LL = 14;
        public const int WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
        public const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
        public const int GWL_EXSTYLE = -20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        const int SM_SWAPBUTTON = 23, LOGPIXELSX = 88;

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }

        public const int WM_QUIT = 0x0012;

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }

        [DllImport("user32.dll")]
        public static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll")]
        public static extern bool TranslateMessage(ref MSG msg);
        [DllImport("user32.dll")]
        public static extern IntPtr DispatchMessage(ref MSG msg);
        [DllImport("user32.dll")]
        public static extern bool PostThreadMessage(uint threadId, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")]
        public static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr dc, int index);

        [DllImport("shell32.dll")]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out IntPtr path);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr ILCreateFromPathW(string path);
        [DllImport("shell32.dll")]
        static extern void ILFree(IntPtr pidl);
        [DllImport("shell32.dll")]
        static extern IntPtr ILFindLastID(IntPtr pidl);
        [DllImport("shell32.dll")]
        static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, IntPtr[] apidl, uint flags);

        public static bool KeyDown(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        public static bool PrimaryButtonDown()
        {
            return KeyDown(GetSystemMetrics(SM_SWAPBUTTON) != 0 ? VK_RBUTTON : VK_LBUTTON);
        }

        // WPF runs system-DPI-aware: physical px / this = DIPs
        public static double DpiScale()
        {
            IntPtr dc = GetDC(IntPtr.Zero);
            try { return GetDeviceCaps(dc, LOGPIXELSX) / 96.0; }
            finally { ReleaseDC(IntPtr.Zero, dc); }
        }

        public static string DownloadsFolder()
        {
            IntPtr p;
            if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out p) == 0)
            {
                string s = Marshal.PtrToStringUni(p);
                Marshal.FreeCoTaskMem(p);
                return s;
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        // Explorer window(s) with the given items selected, one per parent folder
        public static void Reveal(IEnumerable<string> paths)
        {
            foreach (var group in paths.GroupBy(p => Path.GetDirectoryName(p.TrimEnd('\\')) ?? p))
            {
                IntPtr folder = ILCreateFromPathW(group.Key);
                var items = group.Select(p => ILCreateFromPathW(p)).Where(x => x != IntPtr.Zero).ToArray();
                try
                {
                    if (folder == IntPtr.Zero || SHOpenFolderAndSelectItems(folder, (uint)items.Length, items.Select(ILFindLastID).ToArray(), 0) != 0)
                        System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + group.First() + "\"");
                }
                finally
                {
                    foreach (var i in items) ILFree(i);
                    if (folder != IntPtr.Zero) ILFree(folder);
                }
            }
        }
    }
}
