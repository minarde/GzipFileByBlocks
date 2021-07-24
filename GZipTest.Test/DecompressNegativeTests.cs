using GZipTest.Data;
using NUnit.Framework;
using System;
using System.IO;

namespace GZipTest.Test
{
    public class DecompressNegativeTests
    {
        [OneTimeTearDown]
        public void TearDown()
        {
            TestFolders.DeleteTempFolder();
        }

        [Test]
        public void DecompressedFileIsNull()
        {
            Assert.Throws(Is.TypeOf<CompressDecompressFileException>()
                .And.Message.EqualTo("Decompressed file path is empty"),
                () => new FileDecompressor().Decompress(TestFolders.GenerateTempFilePath(), null));
        }

        [Test]
        public void ArchiveFileIsNull()
        {
            Assert.Throws(Is.TypeOf<CompressDecompressFileException>()
                .And.Message.EqualTo("Archive file path is empty"),
                () => new FileDecompressor().Decompress(null, TestFolders.GenerateTempFilePath()));
        }

        [Test]
        public void ArchiveFileDoesNotExist()
        {
            string archiveFilePath = TestFolders.GenerateTempFilePath();
            Assert.Throws(Is.TypeOf<CompressDecompressFileException>()
                .And.Message.EndsWith("does not exist"),
                () => new FileDecompressor().Decompress(archiveFilePath, TestFolders.GenerateTempFilePath()));
        }

        [Test]
        public void DecompressedFileExists()
        {
            // given
            string archiveFilePath = TestFolders.GenerateTempFilePath();
            TestFolders.GenerateFile(archiveFilePath, 1);
            string resultFilePath = TestFolders.GenerateTempFilePath();
            TestFolders.GenerateFile(resultFilePath, 1);

            // when, then
            Assert.Throws(Is.TypeOf<CompressDecompressFileException>()
                .And.Message.EndsWith("exists"),
                () => new FileDecompressor().Decompress(archiveFilePath, resultFilePath));
        }
    }
}