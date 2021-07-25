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
    public class FileCompressor
    {
        public bool Compress(string originalFileName, string archiveFileName, Action<string> writeLog)
        {
            CheckArguments(originalFileName, archiveFileName);

            try
            {
                using Stream source = File.OpenRead(originalFileName);
                writeLog($"Start compressing {originalFileName}");

                long blocksCount = (long)Math.Ceiling((double)source.Length / Constants.BlockSizeBytes);

                using CompressedFileWriter fileWriter = CompressedFile.WriteCompressedFile(archiveFileName, blocksCount, Constants.BlockSizeBytes);

                var result = new ProcessingLogic().Process<CompressBlockData>(
                    (queue, cancellationToken) =>
                    {
                        int counter = 0;
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            byte[] buffer = new byte[Constants.BlockSizeBytes];
                            int readBytes = source.Read(buffer, 0, buffer.Length);
                            if (readBytes <= 0)
                                break;

                            int localCounter = counter;
                            queue.Add(new CompressBlockData(buffer, readBytes, localCounter));

                            counter++;
                        }
                    },
                    (queue, cancellationToken) =>
                    {
                        foreach (CompressBlockData data in queue.GetConsumingEnumerable())
                        {
                            if (cancellationToken.IsCancellationRequested)
                                break;
                            byte[] compressed;
                            using (var compressedMemoryStream = new MemoryStream())
                            {
                                using (var gzipStream = new GZipStream(compressedMemoryStream, CompressionMode.Compress))
                                using (var mStream = new MemoryStream(data.Buffer, 0, data.ReadBytes))
                                    mStream.CopyTo(gzipStream);

                                compressed = compressedMemoryStream.ToArray();
                            }

                            fileWriter.Write(compressed, data.BlockNumber);
                        }
                    },
                    writeLog);

                if (result.Success)
                {
                    fileWriter.WriteFinalInfo();
                    writeLog($"Finish compressing {originalFileName} to {archiveFileName}");
                    return true;
                }
                else
                {
                    writeLog($"Compressing {originalFileName} to {archiveFileName} failed");
                    fileWriter.Close();
                    DeleteResultFileOnException(archiveFileName, writeLog);
                    return false;
                }
            }
            catch (Exception exc)
            {
                writeLog($"Exception during processing: {exc}");
                writeLog($"Compressing {originalFileName} to {archiveFileName} failed");
                return false;
            }
        }

        private static void CheckArguments(string originalFileName, string archiveFileName)
        {
            if (string.IsNullOrWhiteSpace(originalFileName))
                throw new CompressDecompressFileException("Source file path is empty");
            if (string.IsNullOrWhiteSpace(archiveFileName))
                throw new CompressDecompressFileException("Archive file path is empty");
            if (!File.Exists(originalFileName))
                throw new CompressDecompressFileException($"File {originalFileName} does not exist");
            if (File.Exists(archiveFileName))
                throw new CompressDecompressFileException($"File {archiveFileName} exists");
        }

        private static void DeleteResultFileOnException(string archiveFileName, Action<string> writeLog)
        {
            try
            {
                if (File.Exists(archiveFileName))
                    File.Delete(archiveFileName);
            }
            catch (Exception exc)
            {
                writeLog($"Exception during deleting {archiveFileName}, error: {exc.Message}");
            }
        }
    }

    public class CompressBlockData
    {
        public CompressBlockData(byte[] buffer, int readBytes, int blockNumber)
        {
            this.Buffer = buffer;
            this.ReadBytes = readBytes;
            this.BlockNumber = blockNumber;
        }

        public byte[] Buffer { get; private set; }
        public int ReadBytes { get; private set; }
        public int BlockNumber { get; private set; }

        public override string ToString()
        {
            return $"Block number: {BlockNumber}, Read bytes: {ReadBytes}";
        }
    }
}
