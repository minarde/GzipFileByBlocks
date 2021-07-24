using System;
using System.Collections.Generic;
using System.Text;

namespace GZipTest
{
    public class Constants
    {
        public static int BlockSizeBytes = 1024 * 1024;
        public static int ConsumersCount = Environment.ProcessorCount;
        public static int QueueSize = ConsumersCount * 5;
    }
}
