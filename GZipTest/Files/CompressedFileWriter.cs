using GZipTest.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GZipTest.Files
{
    public class CompressedFileWriter : CompressedFile
    {
        private readonly object lockObject = new object();
        private readonly CompressedFileMeta compressedFileInfo;

        public CompressedFileWriter(string filePath, FileStream fileStream, long blocksCount, long blockSizeBytes)
            : base(filePath, fileStream)
        {
            this.compressedFileInfo = new CompressedFileMeta(blockSizeBytes, blocksCount);
            fileStream.Position = compressedFileInfo.GetLength() + FileBeginning.Length + BlocksBeginning.Length + BlocksEnding.Length;
        }

        public void Write(byte[] bytesToWrite, int blockNumber)
        {
            if (isClosed)
                throw new CompressDecompressFileException($"File {filePath} is already closed");
            lock (lockObject)
            {
                fileStream.Write(bytesToWrite, 0, bytesToWrite.Length);
                compressedFileInfo.InsertBlock(new BlockInfo(blockNumber, bytesToWrite.Length));
            }
        }

        public void WriteFinalInfo()
        {
            if (isClosed)
                throw new CompressDecompressFileException($"File {filePath} is already closed");
            lock (lockObject)
            {
                fileStream.Position = 0;

                fileStream.Write(UTF8Encoding.UTF8.GetBytes(FileBeginning));
                Write(compressedFileInfo.BlocksCount);
                Write(compressedFileInfo.BlockSize);
                fileStream.Write(UTF8Encoding.UTF8.GetBytes(BlocksBeginning));
                foreach (BlockInfo blockInfo in compressedFileInfo.InsertedBlocks)
                {
                    Write(blockInfo.OrderNumber);
                    Write(blockInfo.CompressedSize);
                }
                fileStream.Write(UTF8Encoding.UTF8.GetBytes(BlocksEnding));
            }
        }

        private void Write(int number)
        {
            byte[] bytesToWrite = BitConverter.GetBytes(number);
            fileStream.Write(bytesToWrite, 0, bytesToWrite.Length);
        }

        private void Write(long number)
        {
            byte[] bytesToWrite = BitConverter.GetBytes(number);
            fileStream.Write(bytesToWrite, 0, bytesToWrite.Length);
        }
    }
}
