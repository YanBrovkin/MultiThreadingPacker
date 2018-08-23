using System;
using System.IO;
using System.Linq;
using GZipTest.Wrappers;

namespace GZipTest
{
    internal class Program
    {
        private const int _bytesPerWorker = 1048576;

        private static void Main(string[] args)
        {
            if (args == null
                || args.Count() < 3)
            {
                Console.WriteLine("Usage: GZipTest.exe compress/decompress [source file] [destination file]");
                return;
            }
            if (!new FileInfo(args[1]).Exists)
            {
                Console.WriteLine($"File '{args[1]}' not exist");
                return;
            }
            var converter = new ByteLengthConverter();
            var archiver = new GZipWrapper(bytesPerWorker: _bytesPerWorker, converter: converter);
            switch (args[0])
            {
                case null:
                default:
                    return;

                case "compress":
                    Console.Write($"Bytes after compression: {archiver.Compress(args[1], args[2])}");
                    return;

                case "decompress":
                    Console.Write($"Bytes after compression: {archiver.Decompress(args[1], args[2])}");
                    return;
            }
        }
    }
}