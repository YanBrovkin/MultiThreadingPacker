using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using GZipTest.Interfaces;

namespace GZipTest
{
    public class GZipWrapper : ICompressionWrapper
    {
        private readonly int _bytesPerWorker;
        private readonly IByteLengthConverter _converter;
        private Stream _sourceStream;
        private Stream _destStream;

        public GZipWrapper(int bytesPerWorker, IByteLengthConverter converter)
        {
            _bytesPerWorker = bytesPerWorker;
            _converter = converter;
        }

        public int Compress(string sourceFile, string destFile)
        {
            Console.WriteLine($@"[{DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")}] Compression started");
            _sourceStream = GetSourceStream(sourceFile);
            _destStream = GetDestinationStream(destFile);
            var memStreams = GetCompressedStreamsDictionary(_sourceStream);
            var compressedBytes = WriteCompressedStreamsToDestinationStream(_destStream, memStreams);
            Console.WriteLine($@"[{DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")}] Compression finished");
            return compressedBytes;
        }

        public int Decompress(string sourceFile, string destFile)
        {
            Console.WriteLine($@"[{DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")}] Decompression started");
            _sourceStream = GetSourceStream(sourceFile);
            _destStream = GetDestinationStream(destFile);
            var readUncompressedBytes = GetUnCompressedData(_sourceStream);
            var decompressedBytes = WriteUncompressedDataToDestinationStream(_destStream, readUncompressedBytes);
            Console.WriteLine($@"[{DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")}] Decompression finished");
            return decompressedBytes;
        }

        public virtual Stream GetSourceStream(string sourceFile)
            => new FileStream(sourceFile, FileMode.Open);

        public virtual Stream GetDestinationStream(string destinationFile)
            => new FileStream(destinationFile, FileMode.Create);

        public virtual Stream GetZipStream(Stream destStream, CompressionMode compressionMode)
            => new GZipStream(destStream, compressionMode, true);

        private IDictionary<int, MemoryStream> GetCompressedStreamsDictionary(Stream sourceStream)
        {
            var numOfWorkers = (int)(sourceStream.Length / _bytesPerWorker + 1);
            var memStreams = new Dictionary<int, MemoryStream>(numOfWorkers);

            var bufferRead = new byte[_bytesPerWorker];

            var noOfThreadsF = (sourceStream.Length / (float)_bytesPerWorker);
            var noOfThreadsI = (int)sourceStream.Length / _bytesPerWorker;
            float toComp = noOfThreadsI;
            Thread[] threads;
            WaitHandle[] globalWaitHandles;
            if (toComp < noOfThreadsF)
            {
                threads = new Thread[numOfWorkers];
                globalWaitHandles = new WaitHandle[numOfWorkers];
            }
            else
            {
                threads = new Thread[sourceStream.Length / _bytesPerWorker];
                globalWaitHandles = new WaitHandle[sourceStream.Length / _bytesPerWorker];
            }
            var threadsCounter = 0;
            var read = 0;
            var paramSyncEvent = new AutoResetEvent(false);
            while (0 != (read = sourceStream.Read(bufferRead, 0, _bytesPerWorker)))
            {
                var globalSyncEvent = new AutoResetEvent(false);
                threads[threadsCounter] = new Thread(() => CompressStream(bufferRead, read, threadsCounter, memStreams, paramSyncEvent, globalSyncEvent));
                globalWaitHandles[threadsCounter] = globalSyncEvent;
                threads[threadsCounter].Start();
                paramSyncEvent.WaitOne(-1);
                threadsCounter++;
                bufferRead = new byte[_bytesPerWorker];
            }
            WaitHandle.WaitAll(globalWaitHandles);
            sourceStream.Close();
            return memStreams;
        }

        private IDictionary<int, MemoryStream> GetUnCompressedData(Stream sourceStream)
        {
            sourceStream.Seek(0, SeekOrigin.Begin);
            int readLength = 0;
            var readBytesDictionary = new List<byte[]>();
            byte[] buffToReadLength = new byte[8];
            while (0 != (readLength = sourceStream.Read(buffToReadLength, 0, 8)))
            {
                if (readLength != 8)
                    throw new Exception("File corrupted");
                int lengthToRead = _converter.GetLengthFromBytes(buffToReadLength);
                byte[] buffRead = new byte[lengthToRead];

                if (lengthToRead != sourceStream.Read(buffRead, 0, lengthToRead))
                    throw new Exception("File corrupted");
                readBytesDictionary.Add(buffRead);
            }
            var threads = new Thread[readBytesDictionary.Count];
            var memStreamDictionary = new Dictionary<int, MemoryStream>(readBytesDictionary.Count);
            var globalWaitHandles = new WaitHandle[readBytesDictionary.Count];
            var paramSyncEvent = new AutoResetEvent(false);
            for (int counter = 0; counter < readBytesDictionary.Count; counter++)
            {
                var globalSyncEvent = new AutoResetEvent(false);
                threads[counter] = new Thread(() => UnCompressStream(readBytesDictionary[counter], counter, memStreamDictionary, paramSyncEvent, globalSyncEvent));
                globalWaitHandles[counter] = globalSyncEvent;
                threads[counter].Start();
                paramSyncEvent.WaitOne(-1);
            }
            WaitHandle.WaitAll(globalWaitHandles);
            sourceStream.Close();
            return memStreamDictionary;
        }

        private int WriteUncompressedDataToDestinationStream(Stream destStream, IDictionary<int, MemoryStream> uncompressedData)
        {
            var unCompressedBytesCount = 0;
            for (var i = 0; i < uncompressedData.Count(); i++)
            {
                var uncopressedBytesSlice = uncompressedData[i];
                var length = (int)uncopressedBytesSlice.Length;
                byte[] buffToWrite = new byte[length];
                uncopressedBytesSlice.Seek(0, 0);
                uncopressedBytesSlice.Read(buffToWrite, 0, length);
                destStream.Write(buffToWrite, 0, length);
                unCompressedBytesCount += length;
            }
            destStream.Close();
            return unCompressedBytesCount;
        }

        private int WriteCompressedStreamsToDestinationStream(Stream destStream, IDictionary<int, MemoryStream> memStreamDictionary)
        {
            var compressedBytesCount = 0;
            for (var i = 0; i < memStreamDictionary.Count; i++)
            {
                var lengthToStore = _converter.GetBytesFromLength((int)memStreamDictionary[i].Length);
                destStream.Write(lengthToStore, 0, lengthToStore.Length);
                var compressedBytes = memStreamDictionary[i].ToArray();
                memStreamDictionary[i].Close();
                memStreamDictionary[i] = null;
                destStream.Write(compressedBytes, 0, compressedBytes.Length);
                Console.WriteLine($"Part {i} writed {compressedBytes.Length} bytes to destination file");
                compressedBytesCount += compressedBytes.Length;
            }
            destStream.Close();
            return compressedBytesCount;
        }

        private void CompressStream(byte[] bytesToCompress, int length, int index, IDictionary<int, MemoryStream> memStreams, EventWaitHandle syncEvent, EventWaitHandle globalSyncEvent)
        {
            syncEvent.Set();
            var stream = new MemoryStream();
            var gzStream = GetZipStream(stream, CompressionMode.Compress);
            gzStream.Write(bytesToCompress, 0, length);
            gzStream.Close();
            memStreams.Add(index, stream);
            globalSyncEvent.Set();
            Console.WriteLine($"Thread {index} compressed {length} bytes");
        }

        private void UnCompressStream(byte[] bytesToDecompress, int index, IDictionary<int, MemoryStream> memStreams, EventWaitHandle syncEvent, EventWaitHandle globalSyncEvent)
        {
            syncEvent.Set();
            var cmpStream = new MemoryStream(bytesToDecompress);
            var unCompZip = GetZipStream(cmpStream, CompressionMode.Decompress);
            var unCompressedBuffer = new byte[bytesToDecompress.Length];
            var msToAssign = new MemoryStream();
            var read = 0;
            while (0 != (read = unCompZip.Read(unCompressedBuffer, 0, bytesToDecompress.Length)))
            {
                msToAssign.Write(unCompressedBuffer, 0, read);
            }
            memStreams.Add(index, msToAssign);
            var uncompressedLength = msToAssign.Length;
            unCompZip.Close();
            cmpStream.Close();
            globalSyncEvent.Set();
            Console.WriteLine($"Thread {index} decompressed {uncompressedLength} bytes");
        }
    }
}