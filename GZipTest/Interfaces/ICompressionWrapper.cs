namespace GZipTest
{
    public interface ICompressionWrapper
    {
        int Compress(string sourceFile, string destFile);

        int Decompress(string sourceFile, string destFile);
    }
}