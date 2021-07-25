using GZipTest.Data;
using GZipTest.Files;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GZipTest
{
    public class FileDecompressor
    {
        public bool Decompress(string archiveFileName, string decompressingFileName, Action<string> writeLog)
        {
            CheckArguments(archiveFileName, decompressingFileName);

            try
            {
                using CompressedFileReader archiveFile = CompressedFile.ReadCompressedFile(archiveFileName);

                writeLog($"Start decompressing {archiveFileName}");

                var compressedFileInfo = archiveFile.ReadCompressedFileInfo();

                CreateDirectory(decompressingFileName);
                using FileStream decompressedFileStream = File.Create(decompressingFileName);
                object lockObject = new object();

                var result = new ProcessingLogic().Process<DecompressBlockData>(
                    (queue, cancellationToken) =>
                    {
                        foreach (BlockInfo blockInfo in compressedFileInfo.InsertedBlocks)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                break;
                            byte[] buffer = new byte[blockInfo.CompressedSize];
                            int readBytes = archiveFile.Read(buffer);
                            if (readBytes > 0)
                                queue.Add(new DecompressBlockData(buffer, compressedFileInfo.BlockSize, blockInfo));
                        }
                    },
                    (queue, cancellationToken) =>
                    {
                        foreach (DecompressBlockData data in queue.GetConsumingEnumerable())
                        {
                            if (cancellationToken.IsCancellationRequested)
                                break;
                            byte[] decompressed;
                            using (var decompressedMemoryStream = new MemoryStream(data.Buffer, 0, data.BlockInfo.CompressedSize))
                            using (var gzipStream = new GZipStream(decompressedMemoryStream, CompressionMode.Decompress))
                            using (var mStream = new MemoryStream())
                            {
                                gzipStream.CopyTo(mStream);
                                decompressed = mStream.ToArray();
                            }

                            lock (lockObject)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                    break;
                                decompressedFileStream.Position = data.BlocksOriginalSize * data.BlockInfo.OrderNumber;
                                decompressedFileStream.Write(decompressed, 0, decompressed.Length);
                            }
                        }
                    },
                    writeLog);

                if (result.Success)
                {
                    writeLog($"Finish decompressing {archiveFileName} to {decompressingFileName}");
                    return true;
                }
                else
                {
                    writeLog($"Decompressing {archiveFileName} to {decompressingFileName} failed");
                    decompressedFileStream.Close();
                    DeleteResultFileOnException(decompressingFileName, writeLog);
                    return false;
                }
            }
            catch (Exception exc)
            {
                writeLog($"Exception during processing: {exc}");
                writeLog($"Decompressing {archiveFileName} to {decompressingFileName} failed");
                return false;
            }
        }

        private static void CheckArguments(string archiveFileName, string decompressingFileName)
        {
            if (string.IsNullOrWhiteSpace(archiveFileName))
                throw new CompressDecompressFileException("Archive file path is empty");
            if (string.IsNullOrWhiteSpace(decompressingFileName))
                throw new CompressDecompressFileException("Decompressed file path is empty");
            if (!File.Exists(archiveFileName))
                throw new CompressDecompressFileException($"File {archiveFileName} does not exist");
            if (File.Exists(decompressingFileName))
                throw new CompressDecompressFileException($"File {decompressingFileName} exists");
        }

        private static void CreateDirectory(string originalFileName)
        {
            var directoryPath = Path.GetDirectoryName(originalFileName);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
        }

        private static void DeleteResultFileOnException(string decompressingFileName, Action<string> writeLog)
        {
            try
            {
                if (File.Exists(decompressingFileName))
                    File.Delete(decompressingFileName);
            }
            catch (Exception exc)
            {
                writeLog($"Exception during deleting {decompressingFileName}, error: {exc.Message}");
            }
        }
    }

    public class DecompressBlockData
    {
        public DecompressBlockData(byte[] buffer, long blocksOriginalSize, BlockInfo blockInfo)
        {
            this.Buffer = buffer;
            this.BlocksOriginalSize = blocksOriginalSize;
            this.BlockInfo = blockInfo;
        }

        public byte[] Buffer { get; set; }
        public long BlocksOriginalSize { get; private set; }
        public BlockInfo BlockInfo { get; private set; }

        public override string ToString()
        {
            return $"BlockInfo: {BlockInfo}, BlocksOriginalSize: {BlocksOriginalSize}";
        }
    }
}
