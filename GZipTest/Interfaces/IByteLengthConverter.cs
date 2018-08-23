namespace GZipTest.Interfaces
{
    public interface IByteLengthConverter
    {
        byte[] GetBytesFromLength(int length);

        int GetLengthFromBytes(byte[] intToParse);
    }
}