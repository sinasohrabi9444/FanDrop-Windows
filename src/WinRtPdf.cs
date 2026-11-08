using System;
using System.Collections.Generic;
using System.Threading;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace FanDrop
{
    // Windows.Data.Pdf without the System.Runtime.WindowsRuntime await helpers (not available
    // to the in-box compiler), so async operations are waited on by hand.
    static class WinRtPdf
    {
        public static int PageCount(string path)
        {
            var doc = Wait(PdfDocument.LoadFromFileAsync(Wait(StorageFile.GetFileFromPathAsync(path))));
            return (int)doc.PageCount;
        }

        public static List<byte[]> Render(string path, int maxPages = int.MaxValue)
        {
            var doc = Wait(PdfDocument.LoadFromFileAsync(Wait(StorageFile.GetFileFromPathAsync(path))));
            var pages = new List<byte[]>();
            for (uint i = 0; i < doc.PageCount && i < maxPages; i++)
            {
                using (var page = doc.GetPage(i))
                {
                    byte[] png = RenderPage(page, null);
                    int w = PngWidth(png);
                    if (w > 0 && w < 1600)
                    {
                        var o = new PdfPageRenderOptions();
                        o.DestinationWidth = (uint)Math.Min(4000, w * 2);
                        png = RenderPage(page, o);
                    }
                    pages.Add(png);
                }
            }
            return pages;
        }

        static byte[] RenderPage(PdfPage page, PdfPageRenderOptions options)
        {
            var ms = new InMemoryRandomAccessStream();
            if (options == null) Wait(page.RenderToStreamAsync(ms));
            else Wait(page.RenderToStreamAsync(ms, options));
            var reader = new DataReader(ms.GetInputStreamAt(0));
            uint n = Wait(reader.LoadAsync((uint)ms.Size));
            var buf = new byte[n];
            reader.ReadBytes(buf);
            return buf;
        }

        static int PngWidth(byte[] png)
        {
            if (png.Length < 24) return 0;
            return (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        }

        static T Wait<T>(IAsyncOperation<T> op)
        {
            var done = new ManualResetEvent(false);
            op.Completed = delegate { done.Set(); };
            done.WaitOne();
            if (op.Status != AsyncStatus.Completed) throw op.ErrorCode ?? new Exception("operation " + op.Status);
            return op.GetResults();
        }

        static void Wait(IAsyncAction op)
        {
            var done = new ManualResetEvent(false);
            op.Completed = delegate { done.Set(); };
            done.WaitOne();
            if (op.Status != AsyncStatus.Completed) throw op.ErrorCode ?? new Exception("operation " + op.Status);
        }
    }
}
