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
            long blocksCount = ReadLong(fileStream);
            long blocksOriginalSize = ReadLong(fileStream);
            var compressedFileInfo = new CompressedFileMeta(blocksOriginalSize, blocksCount);
            for (int i = 0; i < blocksCount; ++i)
            {
                var blockInfo = new BlockInfo(ReadInt(fileStream), ReadInt(fileStream));
                compressedFileInfo.InsertedBlocks.Add(blockInfo);
            }
            return compressedFileInfo;
        }

        public int Read(byte[] buffer)
        {
            if (isClosed)
                throw new CompressDecompressFileException($"File {filePath} is already closed");
            int readBytes = fileStream.Read(buffer, 0, buffer.Length);
            return readBytes;
        }

        private static long ReadLong(FileStream archiveFileStream)
        {
            byte[] longBytes = new byte[8];
            archiveFileStream.Read(longBytes, 0, 8);
            return BitConverter.ToInt64(longBytes);
        }

        private static int ReadInt(FileStream archiveFileStream)
        {
            byte[] intBytes = new byte[4];
            archiveFileStream.Read(intBytes, 0, 4);
            return BitConverter.ToInt32(intBytes);
        }
    }
}
