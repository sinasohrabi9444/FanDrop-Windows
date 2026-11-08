using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Xml.Linq;

namespace FanDrop
{
    // mac FanDrop used /usr/bin/textutil. Windows has no equivalent, so: Microsoft Word (COM) if
    // installed, else LibreOffice, else a built-in plain-text path that keeps paragraphs.
    static class Docs
    {
        public static void Convert(string src, string format, string dst)
        {
            if (Kinds.Ext(src) == format.ToLowerInvariant())
            {
                File.Copy(src, dst);
                return;
            }
            if (WordConvert(src, format, dst) || OfficeConvert(src, format, dst)) return;
            string text = ReadText(src);
            Write(text, format, dst, Path.GetFileNameWithoutExtension(src));
        }

        public static string ReadText(string path)
        {
            switch (Kinds.Ext(path))
            {
                case "txt": case "md": return ReadPlain(path);
                case "rtf": return RtfText(path);
                case "html": case "htm": return HtmlText(ReadPlain(path));
                case "docx": return DocxText(path);
                case "odt": return OdtText(path);
            }
            string tmp = Images.TempFile(".txt");
            try
            {
                if (WordConvert(path, "TXT", tmp) || OfficeConvert(path, "TXT", tmp)) return ReadPlain(tmp);
            }
            finally { Images.TryDelete(tmp); }
            throw new FanException("can't read ." + Kinds.Ext(path) + " files (install Microsoft Word or LibreOffice)");
        }

        public static int WordCount(string text)
        {
            return Regex.Matches(text, @"[\p{L}\p{N}][\p{L}\p{N}\p{Mn}'’\-_]*").Count;
        }

        // ---- readers ----

        static string ReadPlain(string path)
        {
            using (var r = new StreamReader(path, Encoding.UTF8, true)) return r.ReadToEnd();
        }

        static string RtfText(string path)
        {
            // FlowDocument needs an STA thread; Actions runs jobs on STA threads
            var doc = new FlowDocument();
            var range = new TextRange(doc.ContentStart, doc.ContentEnd);
            using (var fs = File.OpenRead(path)) range.Load(fs, DataFormats.Rtf);
            return range.Text.Replace("\r\n", "\n").TrimEnd('\n');
        }

        static string HtmlText(string html)
        {
            html = Regex.Replace(html, @"<(script|style|head)\b.*?</\1\s*>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"\s+", " ");
            html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</(p|div|li|h[1-6]|tr|blockquote|pre|section|article)\s*>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<li\b[^>]*>", "• ", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<[^>]+>", "");
            html = Regex.Replace(html, @"[ \t\r\f]+", " ");
            html = WebUtility.HtmlDecode(html);
            var lines = html.Split('\n').Select(l => l.Trim());
            return Regex.Replace(string.Join("\n", lines), @"\n{3,}", "\n\n").Trim();
        }

        static string DocxText(string path)
        {
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            using (var zip = ZipFile.OpenRead(path))
            {
                var entry = zip.GetEntry("word/document.xml");
                if (entry == null) throw new FanException("not a valid .docx");
                XDocument x;
                using (var s = entry.Open()) x = XDocument.Load(s);
                var paras = new List<string>();
                foreach (var p in x.Descendants(w + "p"))
                {
                    var sb = new StringBuilder();
                    foreach (var e in p.Descendants())
                    {
                        if (e.Name == w + "t") sb.Append(e.Value);
                        else if (e.Name == w + "tab") sb.Append('\t');
                        else if (e.Name == w + "br" || e.Name == w + "cr") sb.Append('\n');
                    }
                    paras.Add(sb.ToString());
                }
                return string.Join("\n", paras);
            }
        }

        static string OdtText(string path)
        {
            XNamespace t = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
            using (var zip = ZipFile.OpenRead(path))
            {
                var entry = zip.GetEntry("content.xml");
                if (entry == null) throw new FanException("not a valid .odt");
                XDocument x;
                using (var s = entry.Open()) x = XDocument.Load(s);
                var paras = x.Descendants().Where(e => e.Name == t + "p" || e.Name == t + "h").Select(OdtNode);
                return string.Join("\n", paras);
            }
        }

        static string OdtNode(XElement el)
        {
            XNamespace t = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
            var sb = new StringBuilder();
            foreach (var n in el.Nodes())
            {
                var txt = n as XText;
                var e = n as XElement;
                if (txt != null) sb.Append(txt.Value);
                else if (e == null) continue;
                else if (e.Name == t + "s") sb.Append(' ', (int?)e.Attribute(t + "c") ?? 1);
                else if (e.Name == t + "tab") sb.Append('\t');
                else if (e.Name == t + "line-break") sb.Append('\n');
                else sb.Append(OdtNode(e));
            }
            return sb.ToString();
        }

        // ---- writers ----

        static void Write(string text, string format, string dst, string title)
        {
            var paras = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            switch (format)
            {
                case "TXT":
                    File.WriteAllText(dst, string.Join(Environment.NewLine, paras), new UTF8Encoding(true));
                    break;
                case "HTML":
                    {
                        var sb = new StringBuilder("<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n<title>")
                            .Append(WebUtility.HtmlEncode(title)).Append("</title>\n</head>\n<body>\n");
                        foreach (var p in paras)
                            sb.Append(p.Trim().Length == 0 ? "<p>&nbsp;</p>\n" : "<p dir=\"auto\">" + WebUtility.HtmlEncode(p) + "</p>\n");
                        File.WriteAllText(dst, sb.Append("</body>\n</html>\n").ToString(), new UTF8Encoding(false));
                        break;
                    }
                case "RTF":
                    {
                        var sb = new StringBuilder(@"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0 Segoe UI;}}\f0\fs22 ");
                        for (int i = 0; i < paras.Length; i++)
                        {
                            foreach (char c in paras[i])
                            {
                                if (c == '\\' || c == '{' || c == '}') sb.Append('\\').Append(c);
                                else if (c == '\t') sb.Append(@"\tab ");
                                else if (c < 128) sb.Append(c);
                                else sb.Append(@"\u").Append((int)(short)c).Append('?');
                            }
                            if (i < paras.Length - 1) sb.Append(@"\par" + "\n");
                        }
                        File.WriteAllText(dst, sb.Append('}').ToString(), Encoding.ASCII);
                        break;
                    }
                case "DOCX":
                    WriteDocx(paras, dst);
                    break;
                default:
                    throw new FanException("unknown document format " + format);
            }
        }

        static void WriteDocx(string[] paras, string dst)
        {
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            XNamespace xml = "http://www.w3.org/XML/1998/namespace";
            var body = new XElement(w + "body",
                paras.Select(p => new XElement(w + "p",
                    new XElement(w + "r", new XElement(w + "t", new XAttribute(xml + "space", "preserve"), p)))),
                new XElement(w + "sectPr"));
            var doc = new XDocument(new XElement(w + "document", new XAttribute(XNamespace.Xmlns + "w", w), body));

            using (var fs = File.Create(dst))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                Entry(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                    "</Types>");
                Entry(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
                    "</Relationships>");
                Entry(zip, "word/document.xml", doc.Declaration + doc.ToString(SaveOptions.DisableFormatting));
            }
        }

        static void Entry(ZipArchive zip, string name, string content)
        {
            using (var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false))) w.Write(content);
        }

        // ---- external converters ----

        static bool WordConvert(string src, string format, string dst)
        {
            if (Environment.GetEnvironmentVariable("FANDROP_NO_OFFICE") != null) return false;
            Type t = Type.GetTypeFromProgID("Word.Application");
            if (t == null) return false;
            int fileFormat;
            switch (format)
            {
                case "TXT": fileFormat = 2; break;     // wdFormatText (+ UTF-8 encoding below)
                case "RTF": fileFormat = 6; break;
                case "HTML": fileFormat = 10; break;   // filtered HTML
                case "DOCX": fileFormat = 16; break;
                default: return false;
            }
            dynamic word = null;
            try
            {
                word = Activator.CreateInstance(t);
                word.Visible = false;
                word.DisplayAlerts = 0;
                dynamic d = word.Documents.Open(FileName: src, ConfirmConversions: false, ReadOnly: true, AddToRecentFiles: false);
                d.SaveAs2(FileName: dst, FileFormat: fileFormat, AddToRecentFiles: false, Encoding: 65001, AllowSubstitutions: false);
                d.Close(false);
                return File.Exists(dst);
            }
            catch (Exception ex)
            {
                Log.W("Word conversion failed: " + ex.Message);
                return false;
            }
            finally
            {
                if (word != null) try { word.Quit(false); } catch { }
            }
        }

        static bool OfficeConvert(string src, string format, string dst)
        {
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (Environment.GetEnvironmentVariable("FANDROP_NO_OFFICE") != null) return false;
            string soffice = Proc.Find("soffice.exe", Path.Combine(pf, @"LibreOffice\program\soffice.exe"), Path.Combine(pf86, @"LibreOffice\program\soffice.exe"));
            if (soffice == null) return false;
            string filter;
            switch (format)
            {
                case "TXT": filter = "txt:Text (encoded):UTF8"; break;
                case "RTF": filter = "rtf"; break;
                case "HTML": filter = "html"; break;
                case "DOCX": filter = "docx"; break;
                default: return false;
            }
            string outDir = Path.Combine(Path.GetTempPath(), "FanDrop", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outDir);
            try
            {
                string so, se;
                Proc.Exec(soffice, new[] { "--headless", "--convert-to", filter, "--outdir", outDir, src }, out so, out se);
                var made = Directory.GetFiles(outDir).FirstOrDefault();
                if (made == null) { Log.W("LibreOffice conversion failed: " + se); return false; }
                File.Move(made, dst);
                return true;
            }
            finally
            {
                try { Directory.Delete(outDir, true); } catch { }
            }
        }
    }
}
