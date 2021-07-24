using GZipTest.Data;
using NUnit.Framework;
using System;
using System.IO;

namespace GZipTest.Test
{
    public class CompressNegativeTests
    {
        [OneTimeTearDown]
        public void TearDown()
        {
            TestFolders.DeleteTempFolder();
        }

        [Test]
        public void OriginalFileIsNull()
        {
            Assert.Throws(Is.TypeOf<CompressDecompressFileException>()
                .And.Message.EqualTo("Source file path is empty"),
                () => new FileCompressor().Compress(null, TestFolders.GenerateTempFilePath()));
        }

        [Test]
        public void ArchiveFileIsNull()
        {
            Assert.Throws(Is.TypeOf<CompressDecompressFileException>()
                .And.Message.EqualTo("Archive file path is empty"),
                () => new FileCompressor().Compress(TestFolders.GenerateTempFilePath(), null));
        }

        [Test]
        public void OriginalFileDoesNotExist()
        {
            string archiveFilePath = TestFolders.GenerateTempFilePath();
            Assert.Throws(Is.TypeOf<CompressDecompressFileException>()
                .And.Message.EndsWith("does not exist"),
                () => new FileCompressor().Compress(archiveFilePath, TestFolders.GenerateTempFilePath()));
        }

        [Test]
        public void ArchiveFileExists()
        {
            // given
            string originalFilePath = TestFolders.GenerateTempFilePath();
            TestFolders.GenerateFile(originalFilePath, 1);
            string archiveFilePath = TestFolders.GenerateTempFilePath();
            TestFolders.GenerateFile(archiveFilePath, 1);

            // when, then
            Assert.Throws(Is.TypeOf<CompressDecompressFileException>()
                .And.Message.EndsWith("exists"),
                () => new FileCompressor().Compress(originalFilePath, archiveFilePath));
        }

    }
}