﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace GZipTest
{
    public class GZipWrapper : ICompressionWrapper
    {
        private readonly int _maximumThreadsCount;
        private readonly int bytesPerWorker = 1048576;
        private Stream sourceStream;
        private Stream destStream;

        public GZipWrapper(int maximumThreadsCount)
            => _maximumThreadsCount = maximumThreadsCount;

        public int Compress(string sourceFile, string destFile)
        {
            Console.WriteLine($@"[{DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")}] Compression started");

            sourceStream = new FileStream(sourceFile, FileMode.Open);
            destStream = new FileStream(destFile, FileMode.Create);
            var memStreams = GetCompressedStreamsDictionary(sourceStream);
            var compressedBytes = WriteCompressedStreamsToDestinationStream(destStream, memStreams);
            Console.WriteLine($@"[{DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")}] Compression finished");
            return compressedBytes;
        }

        public int Decompress(string sourceFile, string destFile)
        {
            Console.WriteLine($@"[{DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")}] Decompression started");
            sourceStream = new FileStream(sourceFile, FileMode.Open);
            destStream = new FileStream(destFile, FileMode.Create);

            var readUncompressedBytes = GetUnCompressedData(sourceStream);
            var decompressedBytes = WriteUncompressedDataToDestinationStream(destStream, readUncompressedBytes);
            Console.WriteLine($@"[{DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")}] Decompression finished");
            return decompressedBytes;
        }

        private IDictionary<int, MemoryStream> GetCompressedStreamsDictionary(Stream sourceStream)
        {
            var numOfWorkers = (int)(sourceStream.Length / bytesPerWorker + 1);
            var memStreams = new Dictionary<int, MemoryStream>(numOfWorkers);

            var bufferRead = new byte[bytesPerWorker];

            var noOfThreadsF = (sourceStream.Length / (float)bytesPerWorker);
            var noOfThreadsI = (int)sourceStream.Length / bytesPerWorker;
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
                threads = new Thread[sourceStream.Length / bytesPerWorker];
                globalWaitHandles = new WaitHandle[sourceStream.Length / bytesPerWorker];
            }
            var threadsCounter = 0;
            var read = 0;
            var paramSyncEvent = new AutoResetEvent(false);
            while (0 != (read = sourceStream.Read(bufferRead, 0, bytesPerWorker)))
            {
                var globalSyncEvent = new AutoResetEvent(false);
                threads[threadsCounter] = new Thread(() => CompressStream(bufferRead, read, threadsCounter, memStreams, paramSyncEvent, globalSyncEvent));
                globalWaitHandles[threadsCounter] = globalSyncEvent;
                threads[threadsCounter].Start();
                paramSyncEvent.WaitOne(-1);
                threadsCounter++;
                bufferRead = new byte[bytesPerWorker];
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
                int lengthToRead = GetLengthFromBytes(buffToReadLength);
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

        private int WriteUncompressedDataToDestinationStream(Stream destStream, IDictionary<int, MemoryStream> uncopressedData)
        {
            var unCompressedBytesCount = 0;
            for (var i = 0; i < uncopressedData.Count(); i++)
            {
                var uncopressedBytesSlice = uncopressedData[i];
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
                var lengthToStore = GetBytesToStore((int)memStreamDictionary[i].Length);
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
            var gzStream = new GZipStream(stream, CompressionMode.Compress, true);
            gzStream.Write(bytesToCompress, 0, length);
            gzStream.Close();
            memStreams.Add(index, stream);
            globalSyncEvent.Set();
            Console.WriteLine($"Thread {index} compressed {length} bytes");
        }

        private static void UnCompressStream(byte[] bytesToDecompress, int index, Dictionary<int, MemoryStream> memStreams, EventWaitHandle syncEvent, EventWaitHandle globalSyncEvent)
        {
            syncEvent.Set();
            var cmpStream = new MemoryStream(bytesToDecompress);
            var unCompZip = new GZipStream(cmpStream, CompressionMode.Decompress, true);
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

        private byte[] GetBytesToStore(int length)
        {
            int lengthToStore = System.Net.IPAddress.HostToNetworkOrder(length);
            byte[] lengthInBytes = BitConverter.GetBytes(lengthToStore);
            string base64Enc = Convert.ToBase64String(lengthInBytes);
            byte[] finalStore = System.Text.Encoding.ASCII.GetBytes(base64Enc);

            return finalStore;
        }

        private int GetLengthFromBytes(byte[] intToParse)
        {
            string base64Enc = System.Text.Encoding.ASCII.GetString(intToParse);
            byte[] normStr = Convert.FromBase64String(base64Enc);
            int length = BitConverter.ToInt32(normStr, 0);
            return System.Net.IPAddress.NetworkToHostOrder(length);
        }
    }
}