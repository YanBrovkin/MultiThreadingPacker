using FluentAssertions;
using GZipTest.Wrappers;
using NUnit.Framework;

namespace Tests.UnitTests
{
    [TestFixture]
    public class ByteLengthConverterTests
    {
        private readonly ByteLengthConverter _converter = new ByteLengthConverter();

        [Test]
        public void GetBytesFromLength_ShouldReturnProperResult()
        {
            //arrange
            var length = 30;
            //act
            var result = _converter.GetBytesFromLength(length);
            //assert
            result.Should().BeEquivalentTo(new byte[] { 65, 65, 65, 65, 72, 103, 61, 61 });
        }

        [Test]
        public void GetLengthFromBytes_ShouldReturnProperResult()
        {
            //arrange
            var lengthData = new byte[] { 65, 65, 65, 65, 72, 103, 61, 61 };
            //act
            var result = _converter.GetLengthFromBytes(lengthData);
            //assert
            result.Should().Be(30);
        }
    }
}