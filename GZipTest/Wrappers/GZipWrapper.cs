using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace GZipTest
{
    public class GZipWrapper : IArchivator
    {
        private readonly int _bytesPerWorker;
        private static object locker = new object();

        public GZipWrapper(int bytesPerWorker)
            => _bytesPerWorker = bytesPerWorker;

        public int Compress(string sourceFile, string destFile)
        {
            Console.WriteLine($@"[{DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")}] Compression started");
            using (var sourceStream = new FileStream(sourceFile, FileMode.Open))
            using (var resultStream = new FileStream(destFile, FileMode.Create))
            using (var compressedStream = new GZipStream(resultStream, CompressionMode.Compress))
            {
                var fileLength = (int)sourceStream.Length;
                var bytesProcessed = 0;
                while (bytesProcessed < fileLength)
                {
                    var threadsCount = Enumerable.Range(1, 10).Where(p => fileLength % p == 0).Max();
                    var bytesPerWorker = fileLength / threadsCount;
                    var waitHandles = new WaitHandle[threadsCount];
                    for (var i = 0; i < threadsCount; i++)
                    {
                        var handle = new EventWaitHandle(false, EventResetMode.ManualReset);
                        var buffer = new byte[bytesPerWorker];
                        sourceStream.Read(buffer, bytesProcessed, bytesPerWorker);
                        var spec = new WorkerSpec(bytesProcessed, buffer);
                        var worker = new Thread(p =>
                        {
                            var currentThreadId = Thread.CurrentThread.ManagedThreadId;
                            var param = (WorkerSpec)p;
                            lock (locker)
                                compressedStream.Write(param.Buffer, param.Offset, param.Buffer.Length);
                            Console.WriteLine($"Thread {currentThreadId}: wrote {param.Buffer.Length} bytes from offset {param.Offset}");
                            handle.Set();
                        })
                        { IsBackground = true };
                        waitHandles[i] = handle;
                        worker.Start(spec);
                        bytesProcessed += bytesPerWorker;
                    }
                    WaitHandle.WaitAll(waitHandles);
                }
                Console.WriteLine($@"[{DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")}] Compression finished");
                return bytesProcessed;
            }
        }

        public int Decompress(string sourceFile, string destFile)
        {
            Console.WriteLine("Decompress");
            return 0;
        }

        public struct WorkerSpec
        {
            public int Offset { get; }
            public byte[] Buffer { get; }

            public WorkerSpec(int offset, byte[] buffer)
            {
                Offset = offset;
                Buffer = buffer;
            }
        }
    }
}