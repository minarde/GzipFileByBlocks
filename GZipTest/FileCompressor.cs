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

                BlockingCollection<CompressBlockData> queue = new BlockingCollection<CompressBlockData>(Constants.QueueSize);

                var cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = cancellationTokenSource.Token;

                Task producer = Task.Factory.StartNew(() => ProduceFileBlocks(
                    queue,
                    source,
                    cancellationTokenSource,
                    cancellationToken,
                    writeLog));

                List<Task> consumers = new List<Task>();
                for (int i = 0; i < Constants.ConsumersCount; ++i)
                    consumers.Add(Task.Run(() => ConsumeFileBlocks(
                        queue,
                        fileWriter,
                        cancellationTokenSource,
                        cancellationToken,
                        writeLog)));

                try
                {
                    Task.WaitAll(new Task[] { producer }, cancellationToken);
                    queue.CompleteAdding();
                    Task.WaitAll(consumers.ToArray(), cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        writeLog($"Compressing {originalFileName} to {archiveFileName} was cancelled due to error");
                        return false;
                    }
                    else
                    {
                        fileWriter.WriteFinalInfo();
                        writeLog($"Finish compressing {originalFileName} to {archiveFileName}");
                        return true;
                    }
                }
                catch (AggregateException aggrExc)
                {
                    foreach (Exception innerExc in aggrExc.InnerExceptions)
                    {
                        if (innerExc is CompressDecompressFileException)
                            writeLog($"Exception during compressing {originalFileName} to {archiveFileName}: {innerExc.Message}");
                        else if (innerExc is IOException || innerExc is UnauthorizedAccessException)
                            writeLog($"Exception during compressing {originalFileName} to {archiveFileName}, " +
                                $"error during reading or writing file: {innerExc.Message}");
                        else
                            writeLog($"Exception during compressing {originalFileName} to {archiveFileName}: {innerExc}");
                    }
                    writeLog($"Compressing {originalFileName} to {archiveFileName} failed");
                    fileWriter.Close();
                    DeleteResultFileOnException(archiveFileName, writeLog);
                    return false;
                }
                catch (OperationCanceledException ocExc)
                {
                    writeLog($"Compressing {originalFileName} to {archiveFileName} failed: {ocExc.Message}");
                    fileWriter.Close();
                    DeleteResultFileOnException(archiveFileName, writeLog);
                    return false;
                }
            }
            catch (UnauthorizedAccessException accessExc)
            {
                writeLog($"Exception during compressing {originalFileName} to {archiveFileName}, error accessing file: {accessExc.Message}");
                writeLog($"Compressing {originalFileName} to {archiveFileName} failed");
                DeleteResultFileOnException(archiveFileName, writeLog);
                return false;
            }
            catch (IOException ioExc)
            {
                writeLog($"Exception during compressing {originalFileName} to {archiveFileName}, error: {ioExc.Message}");
                writeLog($"Compressing {originalFileName} to {archiveFileName} failed");
                DeleteResultFileOnException(archiveFileName, writeLog);
                return false;
            }
        }

        private static void ProduceFileBlocks(BlockingCollection<CompressBlockData> queue,
            Stream source,
            CancellationTokenSource cancellationTokenSource,
            CancellationToken cancellationToken,
            Action<string> writeLog)
        {
            try
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
            }
            catch (CompressDecompressFileException cdfExc)
            {
                writeLog($"Exception during compressing: {cdfExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (IOException ioExc)
            {
                writeLog($"Exception during compressing, " +
                    $"error during reading or writing file: {ioExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (UnauthorizedAccessException auExc)
            {
                writeLog($"Exception during compressing , " +
                    $"error during reading or writing file: {auExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (Exception exc)
            {
                writeLog($"Exception during compressing: {exc}");
                cancellationTokenSource.Cancel();
            }
        }

        private static void ConsumeFileBlocks(BlockingCollection<CompressBlockData> queue,
            CompressedFileWriter fileWriter,
            CancellationTokenSource cancellationTokenSource,
            CancellationToken cancellationToken,
            Action<string> writeLog)
        {
            try
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
            }
            catch (CompressDecompressFileException cdfExc)
            {
                writeLog($"Exception during compressing: {cdfExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (IOException ioExc)
            {
                writeLog($"Exception during compressing, " +
                    $"error during reading or writing file: {ioExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (UnauthorizedAccessException auExc)
            {
                writeLog($"Exception during compressing , " +
                    $"error during reading or writing file: {auExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (Exception exc)
            {
                writeLog($"Exception during compressing: {exc}");
                cancellationTokenSource.Cancel();
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
