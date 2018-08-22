namespace GZipTest
{
    public interface IArchivator
    {
        int Compress(string sourceFile, string destFile);

        int Decompress(string sourceFile, string destFile);
    }
}