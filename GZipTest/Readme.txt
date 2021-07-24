A command line tool using C# for block-by-block compressing and decompressing of files using class System.IO.Compression.GzipStream.

During compression source file is split by blocks of the same size, block of 1MB.
Each block then is compressed and written to the output file independently of others blocks.

File format:
- 8 bytes - blocks count
- 4 bytes - block original size
- list of blocks
    - 4 bytes - block order number
    - 4 bytes - block compressed size
- blocks data

Usage:
- compressing: GZipTest.exe compress [original file name] [archive file name]
- decompressing: GZipTest.exe decompress [archive file name] [decompressing file name]

On success program should return 0, otherwise 1.

Architecture. Producer-consumer pattern is used to process files.