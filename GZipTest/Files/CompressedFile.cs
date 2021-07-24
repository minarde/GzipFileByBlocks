using GZipTest.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GZipTest.Files
{
    // 8 bytes - format mark (GZipTest)
    // 8 bytes - blocks count
    // 4 bytes - block original size
    // block list
    // 2 bytes - beginning of blocks list (BB)
    //      4 bytes - block order number
    //      4 bytes - block compressed size
    // end of blocks list (BE)
    public abstract class CompressedFile : IDisposable
    {
        protected static readonly string FileBeginning = "GZipTest";
        protected static readonly string BlocksBeginning = "BB";
        protected static readonly string BlocksEnding = "BE";
        protected readonly string filePath;
        protected readonly FileStream fileStream;
        protected bool isClosed;

        protected CompressedFile(string filePath, FileStream fileStream)
        {
            this.filePath = filePath;
            this.fileStream = fileStream;
        }

        public static CompressedFileReader ReadCompressedFile(string filePath)
        {
            FileStream fileStream = File.OpenRead(filePath);
            return new CompressedFileReader(filePath, fileStream);
        }

        public static CompressedFileWriter WriteCompressedFile(string filePath, long blocksCount, long blockSizeBytes)
        {
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            FileStream fileStream = File.Create(filePath);
            return new CompressedFileWriter(filePath, fileStream, blocksCount, blockSizeBytes);
        }

        public void Close()
        {
            if (fileStream != null)
            {
                fileStream.Close();
                isClosed = true;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
