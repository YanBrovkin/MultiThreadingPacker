using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace GZipTest
{
    public class GZipWrapper : IArchivator
    {
        private readonly int _maximumThreadsCount;
        private Stream sourceStream;
        private Stream destStream;
        private static readonly object locker = new object();

        public GZipWrapper(int maximumThreadsCount)
            => _maximumThreadsCount = maximumThreadsCount;

        public int Compress(string sourceFile, string destFile)
        {
            Console.WriteLine($@"[{DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")}] Compression started");
            //using (var sourceStream = new FileStream(sourceFile, FileMode.Open))
            using (var resultStream = new FileStream(destFile, FileMode.Create))
            using (var compressedStream = new GZipStream(resultStream, CompressionMode.Compress, true))
            {
                var fileLength = (int)sourceStream.Length;
                var bytesProcessed = 0;
                while (bytesProcessed < fileLength)
                {
                    var range = Enumerable.Range(1, _maximumThreadsCount);
                    var threadsCount = range.Where(p => fileLength / p >= 1).Max();
                    var bytesPerWorker = fileLength / threadsCount;
                    var waitHandles = new WaitHandle[threadsCount];
                    var threadsInformation = new Dictionary<int, (int Offset, Thread thread)>(threadsCount);
                    for (var i = 0; i < threadsCount; i++)
                    {
                        var handle = new EventWaitHandle(false, EventResetMode.ManualReset);
                        if (i == (threadsCount - 1))
                            bytesPerWorker += fileLength % threadsCount;
                        var buffer = new byte[bytesPerWorker];
                        sourceStream.Seek(bytesProcessed, SeekOrigin.Begin);
                        sourceStream.Read(buffer, 0, buffer.Length);
                        var spec = new WorkerSpec(bytesProcessed, buffer);
                        var worker = new Thread(p =>
                        {
                            var currentThreadId = Thread.CurrentThread.ManagedThreadId;
                            var param = (WorkerSpec)p;
                            lock (locker)
                            {
                                resultStream.Seek(param.Offset, SeekOrigin.Begin);
                                compressedStream.Write(param.Buffer, 0, param.Buffer.Length);
                            }
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

        private void OpenFiles(string sourceFile, string destFile)
        {
            sourceStream = new FileStream(sourceFile, FileMode.Open);
            destStream = new FileStream(destFile, FileMode.Create);
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