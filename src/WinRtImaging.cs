using System;
using System.IO;
using System.Threading;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace FanDrop
{
    // HEIC encoding through Windows.Graphics.Imaging (uses the Store "HEIF Image Extensions" codec)
    static class WinRtImaging
    {
        public static void SaveHeic(byte[] bgra, int width, int height, double dpi, string path)
        {
            Exception error = null;
            var t = new Thread(() =>
            {
                try
                {
                    var ms = new InMemoryRandomAccessStream();
                    var enc = Wait(BitmapEncoder.CreateAsync(BitmapEncoder.HeifEncoderId, ms));
                    enc.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, (uint)width, (uint)height, dpi, dpi, bgra);
                    Wait(enc.FlushAsync());
                    var reader = new DataReader(ms.GetInputStreamAt(0));
                    uint n = Wait(reader.LoadAsync((uint)ms.Size));
                    var buf = new byte[n];
                    reader.ReadBytes(buf);
                    File.WriteAllBytes(path, buf);
                }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();
            t.Join();
            if (error != null) throw error;
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
