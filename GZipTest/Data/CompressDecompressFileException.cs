using System;
using System.Collections.Generic;
using System.Text;

namespace GZipTest.Data
{
    public class CompressDecompressFileException : Exception
    {
        public CompressDecompressFileException(string message)
            : base(message)
        {

        }

    }
}
