using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Media.Imaging;
using Microsoft.VisualBasic.FileIO;

namespace FanDrop
{
    static class Actions
    {
        static readonly string[] EvenSize = { "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2" };

        public static void Run(string slot, FanMode mode, List<string> files)
        {
            Log.W(Slots.ModeName(mode) + " / " + slot + " on " + string.Join(" | ", files));
            // return from the OLE Drop call first so the drag source (Explorer) isn't blocked
            if (mode == FanMode.Actions)
            {
                Host.OnUi(() => Act(slot, files));
                return;
            }
            var t = new Thread(() => Job(slot, mode, files)) { IsBackground = true };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        // synchronous, no UI: used by the --run developer hook
        public static void RunNow(string slot, FanMode mode, List<string> files)
        {
            Log.W("RunNow " + Slots.ModeName(mode) + " / " + slot);
            if (mode == FanMode.Actions) Act(slot, files);
            else Job(slot, mode, files);
        }

        // ---------- Convert / Tools ----------

        static void Job(string slot, FanMode mode, List<string> files)
        {
            var outputs = new List<string>();
            var errors = new List<string>();
            string message = null;
            Host.Busy(true);
            try
            {
                FileKind first = Kinds.Of(files[0]);
                if (first == FileKind.Video || (first == FileKind.Audio && files.Count > 1) || first == FileKind.Pdf)
                    Host.Notify("FanDrop — working…", slot + "  ←  " + Describe(files), null, false);

                if (slot == "ZIP") outputs.Add(Zip(files));
                else if (slot == "SHA-256") message = Sha(files);
                else if (slot == "Copy text" || slot == "Word count") message = TextTool(slot, files, errors);
                else
                {
                    foreach (var f in files)
                    {
                        try
                        {
                            FileKind k = Kinds.Of(f);
                            if (!Slots.For(mode, k).Contains(slot))
                                throw new FanException("\"" + slot + "\" doesn't apply to this file");
                            if (mode == FanMode.Convert) Convert(slot, k, f, outputs);
                            else Tool(slot, k, f, outputs);
                        }
                        catch (Exception ex)
                        {
                            if (!(ex is FanException)) Log.W(slot + " failed on " + f + ": " + ex);
                            errors.Add(Path.GetFileName(f) + ": " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!(ex is FanException)) Log.W(slot + " failed: " + ex);
                errors.Add(ex.Message);
            }
            finally
            {
                Host.Busy(false);
            }

            foreach (var o in outputs) Log.W("wrote " + o);
            foreach (var e in errors) Log.W("error: " + e);

            if (errors.Count > 0)
                Host.Notify(outputs.Count > 0 ? "FanDrop — " + outputs.Count + " saved, " + errors.Count + " failed" : "FanDrop — " + slot + " failed",
                    string.Join("\n", errors), outputs.FirstOrDefault(), true);
            else if (message != null)
                Host.Notify(slot, message, outputs.FirstOrDefault(), false);
            else if (outputs.Count == 1)
                Host.Notify("Saved " + Path.GetFileName(outputs[0]), "Downloads\\FanDrop — click to show it", outputs[0], false);
            else if (outputs.Count > 1)
                Host.Notify("Saved " + outputs.Count + " files", "Downloads\\FanDrop — click to show them", outputs[0], false);
        }

        static void Convert(string slot, FileKind kind, string f, List<string> outputs)
        {
            string ext = slot.ToLowerInvariant();
            switch (kind)
            {
                case FileKind.Image:
                    {
                        string dst = Out(f, "", ext);
                        if (slot == "PDF") Images.SaveAsPdf(Images.Load(f), dst);
                        else if (slot == "WEBP" && Kinds.Ext(f) == "gif")
                            Ffmpeg.Run(f, new[] { "-c:v", "libwebp", "-quality", "85", "-loop", "0" }, dst, "webp"); // keeps animation
                        else Images.Save(Images.Load(f), dst, slot == "WEBP" ? 85 : 92);
                        outputs.Add(dst);
                        break;
                    }
                case FileKind.Video:
                case FileKind.Audio:
                    {
                        string dst = Out(f, "", ext);
                        Ffmpeg.Run(f, ConvertArgs(slot), dst, ext);
                        outputs.Add(dst);
                        break;
                    }
                case FileKind.Document:
                    {
                        string dst = Out(f, "", ext);
                        Docs.Convert(f, slot, dst);
                        outputs.Add(dst);
                        break;
                    }
                case FileKind.Pdf:
                    {
                        var pages = Pdf.RenderPages(f);
                        for (int i = 0; i < pages.Count; i++)
                        {
                            string dst = Out(f, pages.Count > 1 ? " - page " + (i + 1) : "", ext);
                            if (slot == "PNG") File.WriteAllBytes(dst, pages[i]);
                            else
                            {
                                using (var ms = new MemoryStream(pages[i]))
                                {
                                    var frame = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                                    Images.Save(frame, dst, 90);
                                }
                            }
                            outputs.Add(dst);
                        }
                        break;
                    }
                default:
                    throw new FanException("[unsupported] convert/" + slot);
            }
        }

        static string[] ConvertArgs(string slot)
        {
            switch (slot)
            {
                case "MP4": return Concat(EvenSize, "-c:v", "libx264", "-crf", "23", "-preset", "veryfast", "-pix_fmt", "yuv420p", "-c:a", "aac", "-movflags", "+faststart");
                case "MOV": return Concat(EvenSize, "-c:v", "libx264", "-crf", "23", "-preset", "veryfast", "-pix_fmt", "yuv420p", "-c:a", "aac");
                case "GIF": return new[] { "-vf", "fps=12,scale=480:-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse", "-an", "-loop", "0" };
                case "WEBM": return new[] { "-c:v", "libvpx-vp9", "-b:v", "0", "-crf", "34", "-row-mt", "1", "-c:a", "libopus" };
                case "MP3": return new[] { "-vn", "-c:a", "libmp3lame", "-q:a", "2" };
                case "M4A": return new[] { "-vn", "-c:a", "aac", "-b:a", "192k" };
                case "WAV": return new[] { "-vn" };
                case "FLAC": return new[] { "-vn", "-c:a", "flac" };
                case "OGG": return new[] { "-vn", "-c:a", "libopus", "-b:a", "128k" };
            }
            throw new FanException("[unsupported] convert/" + slot);
        }

        static void Tool(string slot, FileKind kind, string f, List<string> outputs)
        {
            string ext = Kinds.Ext(f);
            string dst;
            if (kind == FileKind.Image)
            {
                string same = ext == "heic" || ext == "heif" || ext == "avif" ? "jpg" : Images.CanWrite(ext) ? ext : "png";
                switch (slot)
                {
                    case "Resize 1280": dst = Out(f, " (1280)", same); Images.Save(Images.FitWithin(Images.Load(f), 1280), dst, 92); break;
                    case "Compress": dst = Out(f, " (compressed)", "jpg"); Images.Save(Images.Load(f), dst, 60); break;
                    case "Strip metadata":
                        dst = Out(f, " (clean)", same);
                        if (same != ext || !Images.StripLossless(f, dst)) Images.Save(Images.Load(f), dst, 95);
                        break;
                    case "Rotate 90°": dst = Out(f, " (rotated)", same); Images.Save(Images.Rotate90(Images.Load(f)), dst, 95); break;
                    case "Grayscale": dst = Out(f, " (gray)", same); Images.Save(Images.Grayscale(Images.Load(f)), dst, 92); break;
                    default: throw new FanException("[unsupported] tools/" + slot);
                }
                outputs.Add(dst);
                return;
            }

            if (kind == FileKind.Video)
            {
                switch (slot)
                {
                    case "Compress":
                        dst = Out(f, " (compressed)", "mp4");
                        Ffmpeg.Run(f, Concat(EvenSize, "-c:v", "libx264", "-crf", "28", "-preset", "veryfast", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart"), dst, "compress");
                        break;
                    case "Strip metadata": dst = Out(f, " (clean)", ext); Ffmpeg.Run(f, new[] { "-map", "0", "-map_metadata", "-1", "-map_chapters", "-1", "-c", "copy" }, dst, "strip"); break;
                    case "Mute": dst = Out(f, " (muted)", ext); Ffmpeg.Run(f, new[] { "-an", "-c:v", "copy" }, dst, "mute"); break;
                    case "First 10s": dst = Out(f, " (10s)", ext); Ffmpeg.Run(f, new[] { "-t", "10", "-c", "copy" }, dst, "trim"); break;
                    case "Grab frame": dst = Out(f, " (frame)", "jpg"); Ffmpeg.Run(f, new[] { "-vf", "thumbnail", "-frames:v", "1", "-q:v", "2", "-update", "1" }, dst, "frame"); break;
                    default: throw new FanException("[unsupported] tools/" + slot);
                }
                outputs.Add(dst);
                return;
            }

            if (kind == FileKind.Audio)
            {
                switch (slot)
                {
                    case "Strip metadata": dst = Out(f, " (clean)", ext); Ffmpeg.Run(f, new[] { "-map", "0:a", "-map_metadata", "-1", "-c", "copy" }, dst, "strip"); break;
                    case "Normalize": dst = Out(f, " (normalized)", ext); Ffmpeg.Run(f, Concat(new[] { "-vn", "-af", "loudnorm", "-ar", "48000" }, AudioCodec(ext)), dst, "normalize"); break;
                    case "First 30s": dst = Out(f, " (30s)", ext); Ffmpeg.Run(f, new[] { "-t", "30", "-c", "copy" }, dst, "trim"); break;
                    case "Mono": dst = Out(f, " (mono)", ext); Ffmpeg.Run(f, Concat(new[] { "-vn", "-ac", "1" }, AudioCodec(ext)), dst, "mono"); break;
                    case "Compress": dst = Out(f, " (compressed)", "mp3"); Ffmpeg.Run(f, new[] { "-vn", "-c:a", "libmp3lame", "-b:a", "96k" }, dst, "compress"); break;
                    default: throw new FanException("[unsupported] tools/" + slot);
                }
                outputs.Add(dst);
                return;
            }

            if (kind == FileKind.Pdf && slot == "Strip metadata")
            {
                string exiftool = Proc.Find("exiftool.exe");
                if (exiftool == null) throw new FanException("[unsupported] PDF strip metadata (needs exiftool)");
                dst = Out(f, " (clean)", "pdf");
                string so, se;
                if (Proc.Exec(exiftool, new[] { "-all:all=", "-o", dst, f }, out so, out se) != 0 || !File.Exists(dst))
                    throw new FanException("exiftool failed: " + se.Trim());
                outputs.Add(dst);
                return;
            }

            throw new FanException("[unsupported] tools/" + slot);
        }

        static string[] AudioCodec(string ext)
        {
            switch (ext)
            {
                case "mp3": return new[] { "-c:a", "libmp3lame", "-q:a", "2" };
                case "m4a": case "aac": case "alac": return new[] { "-c:a", "aac", "-b:a", "192k" };
                case "ogg": case "oga": case "opus": return new[] { "-c:a", "libopus", "-b:a", "128k" };
                case "flac": return new[] { "-c:a", "flac" };
                case "wav": case "aiff": case "aif": return new[] { "-c:a", "pcm_s16le" };
            }
            return new string[0];
        }

        static string TextTool(string slot, List<string> files, List<string> errors)
        {
            var texts = new List<string>();
            foreach (var f in files)
            {
                try { texts.Add(Docs.ReadText(f)); }
                catch (Exception ex) { errors.Add(Path.GetFileName(f) + ": " + ex.Message); }
            }
            if (texts.Count == 0) return null;
            string all = string.Join("\n\n", texts);
            if (slot == "Copy text")
            {
                Host.Clipboard(all);
                return "Copied " + all.Length.ToString("N0") + " chars";
            }
            int words = Docs.WordCount(all);
            Host.Clipboard(words.ToString());
            return words.ToString("N0") + " words · " + all.Length.ToString("N0") + " chars (copied)";
        }

        static string Zip(List<string> files)
        {
            string dst = files.Count == 1 ? Out(files[0], "", "zip") : Out("Archive", "", "zip");
            using (var zip = ZipFile.Open(dst, ZipArchiveMode.Create))
            {
                foreach (var f in files)
                {
                    if (Directory.Exists(f)) ZipFolder(zip, f, Path.GetFileName(f.TrimEnd('\\')) + "/");
                    else zip.CreateEntryFromFile(f, Path.GetFileName(f), CompressionLevel.Optimal);
                }
            }
            return dst;
        }

        static void ZipFolder(ZipArchive zip, string dir, string prefix)
        {
            var subdirs = Directory.GetDirectories(dir);
            var files = Directory.GetFiles(dir);
            if (subdirs.Length == 0 && files.Length == 0) zip.CreateEntry(prefix);
            foreach (var file in files) zip.CreateEntryFromFile(file, prefix + Path.GetFileName(file), CompressionLevel.Optimal);
            foreach (var sub in subdirs) ZipFolder(zip, sub, prefix + Path.GetFileName(sub) + "/");
        }

        static string Sha(List<string> files)
        {
            var lines = new List<string>();
            using (var sha = SHA256.Create())
            {
                foreach (var f in files)
                {
                    if (Directory.Exists(f)) throw new FanException("SHA-256 works on files, not folders");
                    using (var fs = File.OpenRead(f))
                        lines.Add(BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant() + (files.Count > 1 ? "  " + Path.GetFileName(f) : ""));
                }
            }
            Host.Clipboard(string.Join(Environment.NewLine, lines));
            return files.Count == 1 ? "sha256 " + lines[0] + " (copied)" : lines.Count + " hashes copied";
        }

        // ---------- Actions (Alt) ----------

        static void Act(string slot, List<string> files)
        {
            try
            {
                switch (slot)
                {
                    case "Copy path":
                        Host.Clipboard(string.Join(Environment.NewLine, files));
                        Host.Notify("FanDrop", "Copied path(s) to clipboard", null, false);
                        break;
                    case "Reveal":
                        Native.Reveal(files);
                        break;
                    case "Open":
                        foreach (var f in files) Host.Shell(f);
                        break;
                    case "Quick Look":
                        new QuickLookWindow(files).Show();
                        break;
                    case "Trash":
                        new Thread(() => Trash(files)) { IsBackground = true }.Start();
                        break;
                    default:
                        Host.Notify("FanDrop", "[stub] actions/" + slot, null, true);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.W(slot + " failed: " + ex);
                Host.Notify("FanDrop — " + slot + " failed", ex.Message, null, true);
            }
        }

        static void Trash(List<string> files)
        {
            var errors = new List<string>();
            foreach (var f in files)
            {
                try
                {
                    if (Directory.Exists(f)) FileSystem.DeleteDirectory(f, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    else FileSystem.DeleteFile(f, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    Log.W("Trash: " + f);
                }
                catch (Exception ex) { errors.Add(Path.GetFileName(f) + ": " + ex.Message); }
            }
            if (errors.Count > 0) Host.Notify("FanDrop — Trash failed", string.Join("\n", errors), null, true);
            else Host.Notify("Moved to Recycle Bin", Describe(files), null, false);
        }

        // ---------- helpers ----------

        static string Describe(List<string> files)
        {
            return files.Count == 1 ? Path.GetFileName(files[0].TrimEnd('\\')) : files.Count + " files";
        }

        static string Out(string src, string suffix, string ext)
        {
            string dir = Host.OutputDir;
            Directory.CreateDirectory(dir);
            string trimmed = src.TrimEnd('\\');
            string name = (Directory.Exists(trimmed) ? Path.GetFileName(trimmed) : Path.GetFileNameWithoutExtension(trimmed)) + suffix;
            string path = Path.Combine(dir, name + "." + ext);
            for (int n = 2; File.Exists(path) || Directory.Exists(path); n++)
                path = Path.Combine(dir, name + " " + n + "." + ext);
            return path;
        }

        static string[] Concat(string[] head, params string[] tail)
        {
            return head.Concat(tail).ToArray();
        }
    }
}
