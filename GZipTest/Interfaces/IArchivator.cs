namespace GZipTest
{
    public interface IArchivator
    {
        byte[] Compress(byte[] source);

        byte[] Decompress(byte[] source);
    }
}