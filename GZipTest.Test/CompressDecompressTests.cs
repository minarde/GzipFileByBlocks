using NUnit.Framework;
using GZipTest;
using System.IO;
using System;
using System.Text;
using System.Security.Cryptography;

namespace GZipTest.Test
{
    public class CompressDecompressTests
    {
        [OneTimeTearDown]
        public void TearDown()
        {
            TestFolders.DeleteTempFolder();
            TestFolders.DeleteArchiveFolder();
            TestFolders.DeleteResultFolder();
        }

        [Test]
        [TestCase("empty_file.test", 0, false)]
        [TestCase("file_less_than_1MB.test", 200 * 1024, false)]
        [TestCase("file_1MB.test", 1024 * 1024, false)]
        [TestCase("file_more_than_1MB.test", 1024 * 1024 + 10, false)]
        [TestCase("file_2MB.test", 2 * 1024 * 1024, false)]
        [TestCase("file_2MB_plus_1.test", 2 * 1024 * 1024 + 1, false)]
        [TestCase("file_2MB_minus_1.test", 2 * 1024 * 1024 - 1, false)]
        [TestCase("file_50MB_minus_1.test", 50 * 1024 * 1024 - 1, false)]
        [TestCase("file_with_zeroes.test", 200 * 1024, true)]
        [Parallelizable(ParallelScope.All)]
        public void CompressAndDecompress(string originalFileName, long originalFileSize, bool isFilledWithZeroes)
        {
            // given
            var originalFilePath = Path.Combine(TestFolders.GenerateTempFilePath(), originalFileName);
            TestFolders.GenerateFile(originalFilePath, originalFileSize);
            long fileSize = new FileInfo(originalFilePath).Length;
            Assert.That(fileSize, Is.EqualTo(originalFileSize));
            var archiveFilePath = TestFolders.GenerateArchiveFilePath();
            var decompressedFilePath = TestFolders.GenerateResultFilePathSameName(originalFileName);
            Console.WriteLine($"Compressing {originalFilePath} to {archiveFilePath} and decompressing to {decompressedFilePath}");

            // when
            // compress
            new FileCompressor().Compress(originalFilePath, archiveFilePath, Console.WriteLine);

            // then
            long archiveFileSize = new FileInfo(archiveFilePath).Length;
            Assert.That(archiveFileSize, Is.GreaterThan(0));

            // when
            // decompress
            new FileDecompressor().Decompress(archiveFilePath, decompressedFilePath, Console.WriteLine);

            // then
            var originalHash = TestFolders.GetFileHash(originalFilePath);
            var decompressedHash = TestFolders.GetFileHash(decompressedFilePath);
            Assert.That(decompressedHash, Is.EqualTo(originalHash));
        }
    }
}