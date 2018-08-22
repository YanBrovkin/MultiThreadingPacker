using System;
using System.IO;
using System.Linq;

namespace GZipTest
{
    internal class Program
    {
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
            var archiver = new GZipWrapper(maximumThreadsCount: 10);
            switch (args[0])
            {
                case null:
                default:
                    return;

                case "compress":
                    Console.Write($"Compressed {archiver.Compress(args[1], args[2])} bytes");
                    return;

                case "decompress":
                    Console.Write($"Decompressed {archiver.Decompress(args[1], args[2])} bytes");
                    return;
            }
        }
    }
}