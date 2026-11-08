using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.Win32;

namespace FanDrop
{
    // Three curved blades around a hub, drawn with GDI+ (the mac menu bar used SF Symbol "fanblades")
    static class IconArt
    {
        static GraphicsPath Blade()
        {
            var p = new GraphicsPath();
            p.AddBezier(new PointF(0, 0), new PointF(-0.42f, -0.30f), new PointF(-0.38f, -0.98f), new PointF(0.10f, -0.97f));
            p.AddBezier(new PointF(0.10f, -0.97f), new PointF(0.55f, -0.88f), new PointF(0.42f, -0.30f), new PointF(0, 0));
            p.CloseFigure();
            return p;
        }

        static void DrawFan(Graphics g, float cx, float cy, float r, Color fg, Color hubCut)
        {
            for (int i = 0; i < 3; i++)
            {
                using (var b = Blade())
                using (var m = new Matrix())
                {
                    m.Translate(cx, cy);
                    m.Rotate(i * 120 + 18);
                    m.Scale(r, r);
                    b.Transform(m);
                    using (var br = new SolidBrush(fg)) g.FillPath(br, b);
                }
            }
            float hub = r * 0.30f, dot = r * 0.13f;
            using (var br = new SolidBrush(hubCut)) g.FillEllipse(br, cx - hub, cy - hub, hub * 2, hub * 2);
            using (var br = new SolidBrush(fg)) g.FillEllipse(br, cx - dot, cy - dot, dot * 2, dot * 2);
        }

        public static Bitmap AppImage(int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float pad = size * 0.04f, s = size - 2 * pad, rad = s * 0.24f;
                using (var path = RoundRect(pad, pad, s, s, rad))
                using (var br = new LinearGradientBrush(new PointF(0, pad), new PointF(0, pad + s), Color.FromArgb(0x2E, 0x9B, 0xFF), Color.FromArgb(0x5E, 0x3C, 0xE6)))
                    g.FillPath(br, path);
                DrawFan(g, size / 2f, size / 2f, s * 0.36f, Color.White, Color.FromArgb(0x47, 0x6C, 0xF2));
            }
            return bmp;
        }

        public static Icon TrayIcon()
        {
            int size = System.Windows.Forms.SystemInformation.SmallIconSize.Width;
            bool lightTaskbar = false;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    lightTaskbar = k != null && Convert.ToInt32(k.GetValue("SystemUsesLightTheme", 0)) == 1;
            }
            catch { }
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingMode = CompositingMode.SourceCopy;
                Color fg = lightTaskbar ? Color.FromArgb(0x1F, 0x1F, 0x1F) : Color.White;
                DrawFan(g, size / 2f, size / 2f, size * 0.47f, fg, Color.Transparent);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        // multi-resolution .ico (PNG-compressed entries) for the exe
        public static void WriteIco(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            var images = new List<byte[]>();
            foreach (int s in sizes)
                using (var bmp = AppImage(s))
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    images.Add(ms.ToArray());
                }
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write((short)0);
                w.Write((short)1);
                w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0);
                    w.Write((byte)0);
                    w.Write((short)1);
                    w.Write((short)32);
                    w.Write(images[i].Length);
                    w.Write(offset);
                    offset += images[i].Length;
                }
                foreach (var img in images) w.Write(img);
            }
        }

        static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
        {
            var p = new GraphicsPath();
            float d = r * 2;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
