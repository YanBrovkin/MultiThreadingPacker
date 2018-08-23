using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using AutoFixture;
using FluentAssertions;
using GZipTest;
using GZipTest.Interfaces;
using Moq;
using NUnit.Framework;

namespace Tests.UnitTests
{
    [TestFixture]
    public class GZipWrapperTests
    {
        private const int _bytesPerWorker = 1048576;
        private Mock<IByteLengthConverter> _converter;
        private Mock<GZipWrapper> _wrapper;
        private Fixture _randomizer;

        [SetUp]
        public void Setup()
        {
            _randomizer = new Fixture();
            _converter = new Mock<IByteLengthConverter>();
            _wrapper = new Mock<GZipWrapper>(_bytesPerWorker, _converter.Object);
        }

        [Test]
        public void Compress_ShouldThrowExceptionWhenCannotReadSourceStream()
        {
            //arrange
            var sourceFile = _randomizer.Create<string>();
            var destFile = _randomizer.Create<string>();
            var sourceStream = new Mock<Stream>();
            sourceStream.Setup(m => m.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Throws(new Exception("Exception"));
            _wrapper.Setup(m => m.GetSourceStream(It.IsAny<string>())).Returns(sourceStream.Object);
            //act
            Action act = () => _wrapper.Object.Compress(sourceFile, destFile);
            //assert
            act.Should().Throw<Exception>().And.Message.Should().Be("Exception");
        }

        [Test]
        public void Compress_ShouldThrowExceptionWhenCannotWriteIntoDestinationStream()
        {
            //arrange
            var sourceFile = _randomizer.Create<string>();
            var destFile = _randomizer.Create<string>();
            var bytes = _randomizer.CreateMany<byte>(20);
            var sourceStream = new MemoryStream(bytes.ToArray());
            _wrapper.Setup(m => m.GetSourceStream(It.IsAny<string>())).Returns(sourceStream);
            var destStream = new Mock<Stream>();
            destStream.Setup(m => m.Write(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Throws(new Exception("Exception"));
            _wrapper.Setup(m => m.GetDestinationStream(It.IsAny<string>())).Returns(destStream.Object);
            _wrapper.Setup(m => m.GetZipStream(It.IsAny<Stream>(), It.IsAny<CompressionMode>())).CallBase();
            //act
            Action act = () => _wrapper.Object.Compress(sourceFile, destFile);
            //assert
            act.Should().Throw<Exception>().And.Message.Should().Be("Exception");
        }

        [Test]
        public void Compress_ShouldProperlyCompressSourceStream()
        {
            //arrange
            var sourceFile = _randomizer.Create<string>();
            var destFile = _randomizer.Create<string>();
            var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 };
            var lengthBytes = new byte[] { 65, 65, 65, 65, 72, 103, 61, 61 };
            _converter.Setup(m => m.GetBytesFromLength(It.IsAny<int>())).Returns(lengthBytes);
            var sourceStream = new MemoryStream(bytes);
            _wrapper.Setup(m => m.GetSourceStream(It.IsAny<string>())).Returns(sourceStream);
            var destStream = new Mock<Stream>();
            _wrapper.Setup(m => m.GetDestinationStream(It.IsAny<string>())).Returns(destStream.Object);
            _wrapper.Setup(m => m.GetZipStream(It.IsAny<Stream>(), It.IsAny<CompressionMode>())).CallBase();
            //act
            var result = _wrapper.Object.Compress(sourceFile, destFile);
            //assert
            destStream.Verify(m => m.Write(lengthBytes, 0, 8), Times.Once); //length of compressed data
            destStream.Verify(m => m.Write(It.IsAny<byte[]>(), 0, 30), Times.Once); //compressed data
            result.Should().Be(30);
        }

        [Test]
        public void Decompress_ShouldThrowExceptionWhenCompressedSliceLengthFromSourceStream_HasWrongLength()
        {
            //arrange
            var sourceFile = _randomizer.Create<string>();
            var destFile = _randomizer.Create<string>();
            var sourceStream = new Mock<Stream>();
            sourceStream.Setup(m => m.Read(It.IsAny<byte[]>(), 0, 8)).Returns(2);
            _wrapper.Setup(m => m.GetSourceStream(It.IsAny<string>())).Returns(sourceStream.Object);
            var destStream = new Mock<Stream>();
            _wrapper.Setup(m => m.GetDestinationStream(It.IsAny<string>())).Returns(destStream.Object);
            _wrapper.Setup(m => m.GetZipStream(It.IsAny<Stream>(), It.IsAny<CompressionMode>())).CallBase();
            //act
            Action act = () => _wrapper.Object.Decompress(sourceFile, destFile);
            //assert
            act.Should().Throw<Exception>().And.Message.Should().Be("File corrupted");
        }

        [Test]
        public void Decompress_ShouldThrowExceptionWhenCompressedSliceFromSourceStream_HasWrongLength()
        {
            //arrange
            var sourceFile = _randomizer.Create<string>();
            var destFile = _randomizer.Create<string>();
            var dataLength = _randomizer.Create<int>();
            var sourceStream = new Mock<Stream>();
            _converter.Setup(m => m.GetLengthFromBytes(It.IsAny<byte[]>())).Returns(dataLength + 1);
            sourceStream.SetupSequence(m => m.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Returns(8).Returns(dataLength);
            _wrapper.Setup(m => m.GetSourceStream(It.IsAny<string>())).Returns(sourceStream.Object);
            var destStream = new Mock<Stream>();
            _wrapper.Setup(m => m.GetDestinationStream(It.IsAny<string>())).Returns(destStream.Object);
            _wrapper.Setup(m => m.GetZipStream(It.IsAny<Stream>(), It.IsAny<CompressionMode>())).CallBase();
            //act
            Action act = () => _wrapper.Object.Decompress(sourceFile, destFile);
            //assert
            act.Should().Throw<Exception>().And.Message.Should().Be("File corrupted");
        }

        [Test]
        public void Decompress_ShouldProperlyDecompressSourceStream()
        {
            //arrange
            var sourceFile = _randomizer.Create<string>();
            var destFile = _randomizer.Create<string>();
            var dataLength = _randomizer.Create<int>();
            var sourceStream = new Mock<Stream>();
            _converter.Setup(m => m.GetLengthFromBytes(It.IsAny<byte[]>())).Returns(dataLength);
            sourceStream.SetupSequence(m => m.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Returns(8).Returns(dataLength);
            _wrapper.Setup(m => m.GetSourceStream(It.IsAny<string>())).Returns(sourceStream.Object);
            var destStream = new Mock<Stream>();
            _wrapper.Setup(m => m.GetDestinationStream(It.IsAny<string>())).Returns(destStream.Object);
            var zipStream = new Mock<Stream>();
            zipStream.SetupSequence(m => m.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Returns(30).Returns(0);
            _wrapper.Setup(m => m.GetZipStream(It.IsAny<Stream>(), It.IsAny<CompressionMode>())).Returns(zipStream.Object);
            //act
            var result = _wrapper.Object.Decompress(sourceFile, destFile);
            //assert
            sourceStream.Verify(m => m.Read(It.IsAny<byte[]>(), 0, 8), Times.Exactly(2)); //length of compressed data
            destStream.Verify(m => m.Write(It.IsAny<byte[]>(), 0, 30), Times.Once); //compressed data
            result.Should().Be(30);
        }
    }
}