using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GZipTest
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args == null || args.Count() < 3)
                return;
            var gzipWrapper = new GZipWrapper();
            switch (args[0])
            {
                case null:
                default:
                    return;

                case "compress":
            }
        }
    }
}