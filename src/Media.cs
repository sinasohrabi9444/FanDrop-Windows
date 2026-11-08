using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FanDrop
{
    static class Proc
    {
        public static string Find(string exe, params string[] extra)
        {
            string here = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new List<string> { Path.Combine(here, exe), Path.Combine(here, "tools", exe) };
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
                if (dir.Trim().Length > 0) candidates.Add(Path.Combine(dir.Trim().Trim('"'), exe));
            candidates.AddRange(extra);
            foreach (var c in candidates)
            {
                try { if (File.Exists(c)) return c; } catch { }
            }
            return null;
        }

        public static string Quote(string a)
        {
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return a;
            var sb = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in a)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') sb.Append('\\', slashes * 2 + 1);
                else sb.Append('\\', slashes);
                slashes = 0;
                sb.Append(c);
            }
            sb.Append('\\', slashes * 2);
            return sb.Append('"').ToString();
        }

        // runs a console tool hidden; returns exit code, collects stdout/stderr (UTF-8)
        public static int Exec(string exe, IEnumerable<string> args, out string stdout, out string stderr)
        {
            var psi = new ProcessStartInfo(exe, string.Join(" ", args.Select(Quote)))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using (var p = Process.Start(psi))
            {
                var err = p.StandardError.ReadToEndAsync();
                stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                stderr = err.Result;
                return p.ExitCode;
            }
        }
    }

    static class Ffmpeg
    {
        static string cached;

        public static string Exe
        {
            get
            {
                if (cached != null && File.Exists(cached)) return cached;
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var extra = new List<string>
                {
                    @"C:\ffmpeg\bin\ffmpeg.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"ffmpeg\bin\ffmpeg.exe"),
                    Path.Combine(home, @"scoop\shims\ffmpeg.exe"),
                    @"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
                    Path.Combine(local, @"Microsoft\WinGet\Links\ffmpeg.exe")
                };
                try
                {
                    string pk = Path.Combine(local, @"Microsoft\WinGet\Packages");
                    if (Directory.Exists(pk))
                        foreach (var d in Directory.GetDirectories(pk, "*FFmpeg*"))
                            extra.AddRange(Directory.GetFiles(d, "ffmpeg.exe", SearchOption.AllDirectories));
                }
                catch { }
                cached = Proc.Find("ffmpeg.exe", extra.ToArray());
                if (cached == null) throw new FanException("ffmpeg not found (winget install Gyan.FFmpeg, or put ffmpeg.exe next to FanDrop.exe)");
                return cached;
            }
        }

        public static void Run(string input, string[] args, string output, string what)
        {
            var all = new List<string> { "-hide_banner", "-nostdin", "-y", "-i", input };
            all.AddRange(args);
            all.Add(output);
            string so, se;
            int code = Proc.Exec(Exe, all, out so, out se);
            if (code != 0 || !File.Exists(output))
            {
                Log.W("ffmpeg " + what + " exit " + code + "\n" + se);
                try { if (File.Exists(output)) File.Delete(output); } catch { }
                var last = se.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).LastOrDefault() ?? "";
                throw new FanException("ffmpeg " + what + " failed (exit " + code + "): " + last);
            }
        }
    }

    static class Images
    {
        // decoded, EXIF-orientation applied, frozen
        public static BitmapSource Load(string path)
        {
            try
            {
                BitmapFrame f;
                using (var fs = File.OpenRead(path))
                {
                    var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    f = dec.Frames[0];
                }
                BitmapSource s = Orient(f, Orientation(f));
                s.Freeze();
                return s;
            }
            catch (Exception ex)
            {
                // codecs Windows lacks (e.g. HEIC/AVIF without the Store extensions): let ffmpeg decode
                Log.W("WIC can't read " + Path.GetFileName(path) + " (" + ex.Message + "), trying ffmpeg");
                string tmp = TempFile(".png");
                try
                {
                    Ffmpeg.Run(path, new[] { "-frames:v", "1", "-update", "1" }, tmp, "decode");
                    using (var fs = File.OpenRead(tmp))
                    {
                        var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                        var f = dec.Frames[0];
                        f.Freeze();
                        return f;
                    }
                }
                finally { TryDelete(tmp); }
            }
        }

        public static int Orientation(BitmapFrame f)
        {
            try
            {
                var md = f.Metadata as BitmapMetadata;
                if (md == null) return 1;
                object o = md.GetQuery("System.Photo.Orientation");
                return o == null ? 1 : Convert.ToInt32(o);
            }
            catch { return 1; }
        }

        static BitmapSource Orient(BitmapSource s, int o)
        {
            Transform t;
            switch (o)
            {
                case 2: t = new ScaleTransform(-1, 1); break;
                case 3: t = new RotateTransform(180); break;
                case 4: t = new ScaleTransform(1, -1); break;
                case 5: t = new TransformGroup { Children = { new ScaleTransform(-1, 1), new RotateTransform(270) } }; break;
                case 6: t = new RotateTransform(90); break;
                case 7: t = new TransformGroup { Children = { new ScaleTransform(-1, 1), new RotateTransform(90) } }; break;
                case 8: t = new RotateTransform(270); break;
                default: return s;
            }
            return new TransformedBitmap(s, t);
        }

        public static BitmapSource Rotate90(BitmapSource s)
        {
            var r = new TransformedBitmap(s, new RotateTransform(90));
            r.Freeze();
            return r;
        }

        public static BitmapSource FitWithin(BitmapSource s, int max)
        {
            int w = s.PixelWidth, h = s.PixelHeight;
            if (Math.Max(w, h) <= max) return s;
            double k = (double)max / Math.Max(w, h);
            int nw = Math.Max(1, (int)Math.Round(w * k)), nh = Math.Max(1, (int)Math.Round(h * k));
            var dv = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.HighQuality);
            using (var dc = dv.RenderOpen()) dc.DrawImage(s, new Rect(0, 0, nw, nh));
            var rtb = new RenderTargetBitmap(nw, nh, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        static byte[] Bgra(BitmapSource s, out int stride)
        {
            var c = new FormatConvertedBitmap(s, PixelFormats.Bgra32, null, 0);
            stride = c.PixelWidth * 4;
            var px = new byte[stride * c.PixelHeight];
            c.CopyPixels(px, stride, 0);
            return px;
        }

        public static BitmapSource OnWhite(BitmapSource s)
        {
            int stride;
            var px = Bgra(s, out stride);
            for (int i = 0; i < px.Length; i += 4)
            {
                int a = px[i + 3];
                if (a == 255) continue;
                for (int c = 0; c < 3; c++) px[i + c] = (byte)((px[i + c] * a + 255 * (255 - a)) / 255);
                px[i + 3] = 255;
            }
            var r = BitmapSource.Create(s.PixelWidth, s.PixelHeight, 96, 96, PixelFormats.Bgra32, null, px, stride);
            r.Freeze();
            return r;
        }

        public static BitmapSource Grayscale(BitmapSource s)
        {
            int stride;
            var px = Bgra(s, out stride);
            for (int i = 0; i < px.Length; i += 4)
            {
                byte y = (byte)Math.Min(255, (int)(0.0722 * px[i] + 0.7152 * px[i + 1] + 0.2126 * px[i + 2] + 0.5));
                px[i] = px[i + 1] = px[i + 2] = y;
            }
            var r = BitmapSource.Create(s.PixelWidth, s.PixelHeight, 96, 96, PixelFormats.Bgra32, null, px, stride);
            r.Freeze();
            return r;
        }

        public static bool CanWrite(string ext)
        {
            return new[] { "png", "jpg", "jpeg", "jfif", "jpe", "tif", "tiff", "bmp", "gif", "webp", "heic", "heif" }.Contains(ext);
        }

        // encodes by extension (webp via ffmpeg, heic via WinRT + the Store HEIF codec)
        public static void Save(BitmapSource s, string path, int jpegQuality)
        {
            string ext = Kinds.Ext(path);
            BitmapEncoder enc;
            switch (ext)
            {
                case "png": enc = new PngBitmapEncoder(); break;
                case "jpg": case "jpeg": case "jfif": case "jpe":
                    enc = new JpegBitmapEncoder { QualityLevel = jpegQuality };
                    s = OnWhite(s);
                    break;
                case "tif": case "tiff": enc = new TiffBitmapEncoder { Compression = TiffCompressOption.Lzw }; break;
                case "bmp": enc = new BmpBitmapEncoder(); s = OnWhite(s); break;
                case "gif": enc = new GifBitmapEncoder(); break;
                case "webp":
                    {
                        string tmp = TempFile(".png");
                        try
                        {
                            Save(s, tmp, 100);
                            Ffmpeg.Run(tmp, new[] { "-c:v", "libwebp", "-quality", jpegQuality.ToString(), "-update", "1" }, path, "webp");
                        }
                        finally { TryDelete(tmp); }
                        return;
                    }
                case "heic": case "heif":
                    {
                        int stride;
                        var px = Bgra(s, out stride);
                        try { WinRtImaging.SaveHeic(px, s.PixelWidth, s.PixelHeight, 96, path); }
                        catch (Exception ex)
                        {
                            TryDelete(path);
                            Log.W("HEIC encode failed: " + ex);
                            throw new FanException("HEIC encoding failed (" + ex.Message.Trim() + ") \u2014 needs \"HEIF Image Extensions\" + \"HEVC Video Extensions\" from the Microsoft Store");
                        }
                        return;
                    }
                default: throw new FanException("can't write ." + ext + " images");
            }
            enc.Frames.Add(BitmapFrame.Create(s));
            try
            {
                using (var fs = File.Create(path)) enc.Save(fs);
            }
            catch
            {
                TryDelete(path);
                throw;
            }
        }

        public static void SaveAsPdf(BitmapSource s, string path)
        {
            var white = OnWhite(s);
            var enc = new JpegBitmapEncoder { QualityLevel = 92 };
            enc.Frames.Add(BitmapFrame.Create(white));
            byte[] jpg;
            using (var ms = new MemoryStream()) { enc.Save(ms); jpg = ms.ToArray(); }
            double dpi = s.DpiX >= 50 && s.DpiX <= 1200 ? s.DpiX : 96;
            Pdf.WriteSingleImage(jpg, s.PixelWidth, s.PixelHeight, dpi, path);
        }

        // lossless metadata removal for JPEG/PNG; returns false if not applicable
        public static bool StripLossless(string src, string dst)
        {
            byte[] b = File.ReadAllBytes(src);
            string ext = Kinds.Ext(src);
            if ((ext == "jpg" || ext == "jpeg" || ext == "jfif" || ext == "jpe") && b.Length > 4 && b[0] == 0xFF && b[1] == 0xD8)
            {
                using (var fs = File.OpenRead(src))
                {
                    var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                    if (Orientation(dec.Frames[0]) != 1) return false; // needs re-encode to keep it upright
                }
                var o = new MemoryStream();
                o.Write(b, 0, 2);
                int i = 2;
                while (i + 4 <= b.Length && b[i] == 0xFF)
                {
                    byte m = b[i + 1];
                    if (m == 0xDA) break;
                    int len = (b[i + 2] << 8) | b[i + 3];
                    // drop APP1 (EXIF/XMP), APP12-13 (IPTC/Photoshop), COM; keep JFIF, ICC (APP2), Adobe (APP14)
                    bool drop = m == 0xE1 || m == 0xEC || m == 0xED || m == 0xFE || (m >= 0xE3 && m <= 0xEB);
                    if (!drop) o.Write(b, i, len + 2);
                    i += len + 2;
                }
                o.Write(b, i, b.Length - i);
                File.WriteAllBytes(dst, o.ToArray());
                return true;
            }
            if (ext == "png" && b.Length > 8 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G')
            {
                var o = new MemoryStream();
                o.Write(b, 0, 8);
                int i = 8;
                while (i + 12 <= b.Length)
                {
                    int len = (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
                    string type = Encoding.ASCII.GetString(b, i + 4, 4);
                    bool drop = type == "tEXt" || type == "zTXt" || type == "iTXt" || type == "eXIf" || type == "tIME";
                    if (!drop) o.Write(b, i, len + 12);
                    i += len + 12;
                }
                File.WriteAllBytes(dst, o.ToArray());
                return true;
            }
            return false;
        }

        public static string TempFile(string ext)
        {
            string dir = Path.Combine(Path.GetTempPath(), "FanDrop");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, Guid.NewGuid().ToString("N") + ext);
        }

        public static void TryDelete(string p)
        {
            try { if (File.Exists(p)) File.Delete(p); } catch { }
        }
    }

    static class Pdf
    {
        public static void WriteSingleImage(byte[] jpeg, int pxW, int pxH, double dpi, string path)
        {
            var ci = CultureInfo.InvariantCulture;
            string w = (pxW * 72.0 / dpi).ToString("0.###", ci), h = (pxH * 72.0 / dpi).ToString("0.###", ci);
            string content = "q " + w + " 0 0 " + h + " 0 0 cm /Im0 Do Q";
            var objs = new List<byte[]>
            {
                Ascii("<< /Type /Catalog /Pages 2 0 R >>"),
                Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
                Ascii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + w + " " + h + "] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>"),
                Concat(Ascii("<< /Type /XObject /Subtype /Image /Width " + pxW + " /Height " + pxH +
                             " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " + jpeg.Length + " >>\nstream\n"),
                       jpeg, Ascii("\nendstream")),
                Ascii("<< /Length " + content.Length + " >>\nstream\n" + content + "\nendstream")
            };
            using (var fs = File.Create(path))
            {
                var offsets = new List<long>();
                Write(fs, Ascii("%PDF-1.4\n%\u00e2\u00e3\u00cf\u00d3\n"));
                for (int i = 0; i < objs.Count; i++)
                {
                    offsets.Add(fs.Position);
                    Write(fs, Ascii((i + 1) + " 0 obj\n"));
                    Write(fs, objs[i]);
                    Write(fs, Ascii("\nendobj\n"));
                }
                long xref = fs.Position;
                var sb = new StringBuilder("xref\n0 " + (objs.Count + 1) + "\n0000000000 65535 f \n");
                foreach (var o in offsets) sb.Append(o.ToString("D10")).Append(" 00000 n \n");
                sb.Append("trailer\n<< /Size " + (objs.Count + 1) + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
                Write(fs, Ascii(sb.ToString()));
            }
        }

        // renders every page to PNG bytes via the built-in Windows.Data.Pdf engine (~2x scale)
        public static List<byte[]> RenderPages(string path)
        {
            List<byte[]> result = null;
            Exception error = null;
            var t = new Thread(() =>
            {
                try { result = WinRtPdf.Render(path); }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();
            t.Join();
            if (error != null) throw new FanException("couldn't render PDF: " + error.Message);
            return result;
        }

        static byte[] Ascii(string s) { return Encoding.GetEncoding(28591).GetBytes(s); }
        static void Write(Stream s, byte[] b) { s.Write(b, 0, b.Length); }
        static byte[] Concat(params byte[][] parts) { return parts.SelectMany(p => p).ToArray(); }
    }
}
