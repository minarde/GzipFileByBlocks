using GZipTest.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GZipTest.Files
{
    public class CompressedFileReader : CompressedFile
    {
        public CompressedFileReader(string filePath, FileStream fileStream)
            : base(filePath, fileStream)
        {
        }

        public CompressedFileMeta ReadCompressedFileInfo()
        {
            if (isClosed)
                throw new CompressDecompressFileException($"File {filePath} is already closed");

            fileStream.Position = 0;

            string fileBeginningMark = ReadStringOfSize(8);
            if (string.Compare(fileBeginningMark, FileBeginning) != 0)
                throw new CompressDecompressFileException($"File {filePath} has wrong format");

            long blocksCount = ReadLong();
            long blocksOriginalSize = ReadLong();

            var blocksBeginningMark = ReadStringOfSize(2);
            if (string.Compare(blocksBeginningMark, BlocksBeginning) != 0)
                throw new CompressDecompressFileException($"File {filePath} has wrong format");

            var compressedFileInfo = new CompressedFileMeta(blocksOriginalSize, blocksCount);
            for (int i = 0; i < blocksCount; ++i)
            {
                var blockInfo = new BlockInfo(ReadInt(), ReadInt());
                compressedFileInfo.InsertedBlocks.Add(blockInfo);
            }

            var blocksEndingMark = ReadStringOfSize(2);
            if (string.Compare(blocksEndingMark, BlocksEnding) != 0)
                throw new CompressDecompressFileException($"File {filePath} has wrong format");

            return compressedFileInfo;
        }

        private string ReadStringOfSize(int size)
        {
            byte[] stringBytes = new byte[size];
            fileStream.Read(stringBytes, 0, size);
            return UTF8Encoding.UTF8.GetString(stringBytes);
        }

        public int Read(byte[] buffer)
        {
            if (isClosed)
                throw new CompressDecompressFileException($"File {filePath} is already closed");
            int readBytes = fileStream.Read(buffer, 0, buffer.Length);
            return readBytes;
        }

        private long ReadLong()
        {
            byte[] longBytes = new byte[8];
            fileStream.Read(longBytes, 0, 8);
            return BitConverter.ToInt64(longBytes);
        }

        private int ReadInt()
        {
            byte[] intBytes = new byte[4];
            fileStream.Read(intBytes, 0, 4);
            return BitConverter.ToInt32(intBytes);
        }
    }
}
