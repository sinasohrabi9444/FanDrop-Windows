using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace FanDrop
{
    // Files from Explorer arrive as FileDrop; browsers / Outlook hand over "virtual" files
    // (FileGroupDescriptorW + FileContents), the Windows counterpart of mac file promises.
    static class DropData
    {
        const string Descriptor = "FileGroupDescriptorW";

        public static List<string> Names(IDataObject data)
        {
            try
            {
                var files = data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0) return files.ToList();
                if (data.GetDataPresent(Descriptor)) return VirtualNames(data);
            }
            catch (Exception ex)
            {
                Log.W("reading drag data failed: " + ex.Message);
            }
            return new List<string>();
        }

        public static List<string> Resolve(IDataObject data)
        {
            var files = data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0) return files.ToList();
            if (!data.GetDataPresent(Descriptor)) return new List<string>();

            var names = VirtualNames(data);
            if (names.Count == 0) return names;
            var contents = data.GetData("FileContents") as Stream;
            if (contents == null) throw new FanException("the app didn't hand over the file contents");
            string dir = Path.Combine(Path.GetTempPath(), "FanDrop", "drops", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, Safe(names[0]));
            using (var fs = File.Create(path)) contents.CopyTo(fs);
            Log.W("promised-file drag: " + path);
            if (names.Count > 1) Log.W("only the first of " + names.Count + " virtual files is supported");
            return new List<string> { path };
        }

        static List<string> VirtualNames(IDataObject data)
        {
            var result = new List<string>();
            var ms = data.GetData(Descriptor) as MemoryStream;
            if (ms == null) return result;
            byte[] b = ms.ToArray();
            if (b.Length < 4) return result;
            int count = BitConverter.ToInt32(b, 0);
            const int itemSize = 592, nameOffset = 72;
            for (int i = 0; i < count; i++)
            {
                int o = 4 + i * itemSize + nameOffset;
                if (o + 520 > b.Length) break;
                string name = Encoding.Unicode.GetString(b, o, 520);
                int z = name.IndexOf('\0');
                if (z >= 0) name = name.Substring(0, z);
                if (name.Length > 0) result.Add(name);
            }
            return result;
        }

        static string Safe(string name)
        {
            name = Path.GetFileName(name.Replace('/', '\\'));
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Length == 0 ? "dropped" : name;
        }
    }
}
