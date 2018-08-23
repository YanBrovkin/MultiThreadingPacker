using System;
using GZipTest.Interfaces;

namespace GZipTest.Wrappers
{
    public class ByteLengthConverter : IByteLengthConverter
    {
        public byte[] GetBytesFromLength(int length)
        {
            var lengthToStore = System.Net.IPAddress.HostToNetworkOrder(length);
            var lengthInBytes = BitConverter.GetBytes(lengthToStore);
            var base64Enc = Convert.ToBase64String(lengthInBytes);
            return System.Text.Encoding.ASCII.GetBytes(base64Enc);
        }

        public int GetLengthFromBytes(byte[] intToParse)
        {
            var base64Enc = System.Text.Encoding.ASCII.GetString(intToParse);
            var normStr = Convert.FromBase64String(base64Enc);
            var length = BitConverter.ToInt32(normStr, 0);
            return System.Net.IPAddress.NetworkToHostOrder(length);
        }
    }
}