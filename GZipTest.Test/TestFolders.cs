using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GZipTest.Test
{
    class TestFolders
    {
        private static readonly Random rnd = new Random();

        private static readonly string archiveFilesFolder = @"archive";
        private static readonly string resultFilesFolder = @"decompress";
        private static readonly string tempFilesFolder = @"temp";

        public static string GenerateArchiveFilePath()
        {
            return Path.Combine(archiveFilesFolder, Guid.NewGuid().ToString() + ".tmp");
        }

        public static string GetTempFolderPath()
        {
            return tempFilesFolder;
        }

        public static string GenerateTempFilePath()
        {
            return Path.Combine(tempFilesFolder, Guid.NewGuid().ToString() + ".tmp");
        }

        public static string GenerateResultFilePath(string directory, string extension)
        {
            return Path.Combine(resultFilesFolder, Guid.NewGuid().ToString() + extension);
        }

        public static string GenerateResultFilePathSameName(string originalFileName)
        {
            return Path.Combine(resultFilesFolder, "decompressed" + originalFileName);
        }

        public static void DeleteTempFolder()
        {
            if (Directory.Exists(tempFilesFolder))
                Directory.Delete(tempFilesFolder, true);
        }

        public static void DeleteArchiveFolder()
        {
            if (Directory.Exists(archiveFilesFolder))
                Directory.Delete(archiveFilesFolder, true);
        }

        public static void DeleteResultFolder()
        {
            if (Directory.Exists(resultFilesFolder))
                Directory.Delete(resultFilesFolder, true);
        }

        public static void GenerateFile(string filePath, long size, bool isFilledWithZeroes = false)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            using FileStream file = File.Create(filePath);
            long fileSize = 0;
            int blockSize = 1024 * 1024;
            long counter = size / blockSize;
            if (counter > 0)
            {
                for (int i = 0; i < counter; ++i)
                {
                    byte[] bytes = new byte[blockSize];
                    if (!isFilledWithZeroes)
                        rnd.NextBytes(bytes);
                    file.Write(bytes, 0, bytes.Length);
                    file.Flush();
                    fileSize += blockSize;
                }
            }
            {
                byte[] bytes = new byte[size - fileSize];
                if (!isFilledWithZeroes)
                    rnd.NextBytes(bytes);
                file.Write(bytes, 0, bytes.Length);
            }
        }

        public static string GetFileHash(string filename)
        {
            var hash = new SHA1Managed();
            var clearBytes = File.ReadAllBytes(filename);
            var hashedBytes = hash.ComputeHash(clearBytes);
            return BitConverter.ToString(hashedBytes);
        }
    }
}
