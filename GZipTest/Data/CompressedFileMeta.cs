using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace GZipTest.Data
{
    public class CompressedFileMeta
    {
        public CompressedFileMeta(long blockSize, long blocksCount)
        {
            this.BlockSize = blockSize;
            this.BlocksCount = blocksCount;
            this.InsertedBlocks = new List<BlockInfo>();
        }

        public long BlockSize { get; private set; }

        public long BlocksCount { get; private set; }

        public List<BlockInfo> InsertedBlocks { get; private set; }

        public long GetLength()
        {
            return 8 * 2 + 4 * 2 * BlocksCount;
        }

        public void InsertBlock(BlockInfo blockInfo)
        {
            InsertedBlocks.Add(blockInfo);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder($"blocksCount: {BlocksCount}\n" +
                $"blocksOriginalSize: {BlockSize}\n");
            foreach (BlockInfo blockInfo in InsertedBlocks)
            {
                sb.Append($"{blockInfo}\n");
            }
            return sb.ToString();
        }
    }
}
