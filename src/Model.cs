using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;

namespace FanDrop
{
    enum FileKind { Image, Video, Audio, Document, Pdf, Other }

    // Shift -> Convert, Ctrl -> Tools, Alt -> Actions (mac: Shift / Command / Control)
    enum FanMode { Convert, Tools, Actions }

    class FanException : Exception
    {
        public FanException(string msg) : base(msg) { }
    }

    static class Kinds
    {
        static readonly HashSet<string> Img = Set("png jpg jpeg jfif jpe gif bmp tif tiff webp heic heif avif ico");
        static readonly HashSet<string> Vid = Set("mp4 mov m4v mkv avi webm wmv flv mpg mpeg 3gp ts mts m2ts ogv");
        static readonly HashSet<string> Aud = Set("mp3 m4a wav flac ogg oga opus aac wma aiff aif caf alac amr");
        static readonly HashSet<string> Doc = Set("doc docx rtf txt html htm odt md pages");

        static HashSet<string> Set(string s)
        {
            return new HashSet<string>(s.Split(' '), StringComparer.OrdinalIgnoreCase);
        }

        public static string Ext(string path)
        {
            return Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        }

        public static FileKind Of(string path)
        {
            if (Directory.Exists(path)) return FileKind.Other;
            string e = Ext(path);
            if (e == "pdf") return FileKind.Pdf;
            if (Img.Contains(e)) return FileKind.Image;
            if (Vid.Contains(e)) return FileKind.Video;
            if (Aud.Contains(e)) return FileKind.Audio;
            if (Doc.Contains(e)) return FileKind.Document;
            return FileKind.Other;
        }
    }

    static class Slots
    {
        public static string[] For(FanMode mode, FileKind kind)
        {
            if (mode == FanMode.Actions)
                return new[] { "Copy path", "Reveal", "Open", "Quick Look", "Trash" };

            if (mode == FanMode.Convert)
            {
                switch (kind)
                {
                    case FileKind.Image: return new[] { "PNG", "JPG", "WEBP", "HEIC", "TIFF", "PDF" };
                    case FileKind.Video: return new[] { "MP4", "MOV", "GIF", "WEBM", "MP3" };
                    case FileKind.Audio: return new[] { "MP3", "M4A", "WAV", "FLAC", "OGG" };
                    case FileKind.Document: return new[] { "TXT", "RTF", "HTML", "DOCX" };
                    case FileKind.Pdf: return new[] { "PNG", "JPG" };
                    default: return new[] { "ZIP", "SHA-256" };
                }
            }

            switch (kind)
            {
                case FileKind.Image: return new[] { "Resize 1280", "Compress", "Strip metadata", "Rotate 90°", "Grayscale" };
                case FileKind.Video: return new[] { "Compress", "Strip metadata", "Mute", "First 10s", "Grab frame" };
                case FileKind.Audio: return new[] { "Strip metadata", "Normalize", "First 30s", "Mono", "Compress" };
                case FileKind.Document: return new[] { "Copy text", "Word count", "ZIP" };
                case FileKind.Pdf: return new[] { "ZIP", "Strip metadata" };
                default: return new[] { "ZIP", "SHA-256" };
            }
        }

        // Segoe Fluent Icons / Segoe MDL2 Assets glyphs (stand-ins for the mac SF Symbols)
        public static string Icon(string label)
        {
            switch (label)
            {
                case "PNG": case "JPG": case "WEBP": case "HEIC": case "TIFF": return "";
                case "PDF": case "TXT": case "DOCX": return "";
                case "RTF": return "";
                case "HTML": return "";
                case "MP4": case "MOV": case "WEBM": return "";
                case "GIF": return "";
                case "MP3": case "M4A": case "WAV": case "FLAC": case "OGG": return "";
                case "ZIP": return "";
                case "SHA-256": return "";
                case "Resize 1280": return "";
                case "Compress": return "";
                case "Strip metadata": return "";
                case "Rotate 90°": return "";
                case "Grayscale": return "";
                case "Mute": return "";
                case "First 10s": case "First 30s": return "";
                case "Grab frame": return "";
                case "Normalize": return "";
                case "Mono": return "";
                case "Copy text": return "";
                case "Word count": return "";
                case "Copy path": return "";
                case "Reveal": return "";
                case "Open": return "";
                case "Quick Look": return "";
                case "Trash": return "";
            }
            return "";
        }

        public static string ModeName(FanMode m)
        {
            switch (m)
            {
                case FanMode.Convert: return "Convert";
                case FanMode.Tools: return "Tools";
                default: return "Actions";
            }
        }

        public static string ModeKey(FanMode m)
        {
            switch (m)
            {
                case FanMode.Convert: return "Shift";
                case FanMode.Tools: return "Ctrl";
                default: return "Alt";
            }
        }

        public static Color Accent(FanMode m)
        {
            switch (m)
            {
                case FanMode.Convert: return Color.FromRgb(0x0A, 0x84, 0xFF);
                case FanMode.Tools: return Color.FromRgb(0xFF, 0x9F, 0x0A);
                default: return Color.FromRgb(0xBF, 0x5A, 0xF2);
            }
        }
    }
}
