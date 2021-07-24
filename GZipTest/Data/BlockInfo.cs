using System;
using System.Collections.Generic;
using System.Text;

namespace GZipTest.Data
{
    public struct BlockInfo
    {
        public BlockInfo(int orderNumber, int compressedSize)
        {
            this.OrderNumber = orderNumber;
            this.CompressedSize = compressedSize;
        }

        public int OrderNumber { get; private set; }

        public int CompressedSize { get; private set; }

        public override string ToString()
        {
            return $"block number {OrderNumber}: {CompressedSize}";
        }
    }
}
