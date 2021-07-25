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
                BlockingCollection<DecompressBlockData> queue = new BlockingCollection<DecompressBlockData>(Constants.QueueSize);
                object lockObject = new object();

                var cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = cancellationTokenSource.Token;

                Task producer = Task.Factory.StartNew(() => ProduceBlocks(
                    queue,
                    archiveFile,
                    compressedFileInfo,
                    cancellationTokenSource,
                    cancellationToken,
                    writeLog));

                List<Task> consumers = new List<Task>();
                for (int i = 0; i < Constants.ConsumersCount; ++i)
                    consumers.Add(Task.Run(() => ConsumeFileBlocks(
                        queue,
                        decompressedFileStream,
                        lockObject,
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
                        writeLog($"Decompressing {archiveFileName} to {decompressingFileName} was cancelled due to error");
                        return false;
                    }
                    else
                    {
                        writeLog($"Finish decompressing {archiveFileName} to {decompressingFileName}");
                        return true;
                    }
                }
                catch(OperationCanceledException ocExc)
                {
                    writeLog($"Decompressing {archiveFileName} to {decompressingFileName} failed: {ocExc.Message}");
                    decompressedFileStream.Close();
                    DeleteResultFileOnException(decompressingFileName, writeLog);
                    return false;
                }
                catch (AggregateException aggrExc)
                {
                    foreach (Exception innerExc in aggrExc.InnerExceptions)
                    {
                        if (innerExc is CompressDecompressFileException)
                            writeLog($"Exception during decompressing {archiveFileName} to {decompressingFileName}: {innerExc.Message}");
                        else if (innerExc is IOException || innerExc is UnauthorizedAccessException)
                            writeLog($"Exception during decompressing {archiveFileName} to {decompressingFileName}, " +
                                $"error during reading or writing file: {innerExc.Message}");
                        else
                            writeLog($"Exception during decompressing {archiveFileName} to {decompressingFileName}: {innerExc}");
                    }
                    writeLog($"Decompressing {archiveFileName} to {decompressingFileName} failed");
                    decompressedFileStream.Close();
                    DeleteResultFileOnException(decompressingFileName, writeLog);
                    return false;
                }
            }
            catch (UnauthorizedAccessException accessExc)
            {
                writeLog($"Exception during decompressing {archiveFileName} to {decompressingFileName}, error accessing file: {accessExc.Message}");
                writeLog($"Decompressing {archiveFileName} to {decompressingFileName} failed");
                DeleteResultFileOnException(decompressingFileName, writeLog);
                return false;
            }
            catch (IOException ioExc)
            {
                writeLog($"Exception during decompressing {archiveFileName} to {decompressingFileName}, error: {ioExc.Message}");
                writeLog($"Decompressing {archiveFileName} to {decompressingFileName} failed");
                DeleteResultFileOnException(decompressingFileName, writeLog);
                return false;
            }
        }

        private static void ProduceBlocks(BlockingCollection<DecompressBlockData> queue,
            CompressedFileReader archiveFile,
            CompressedFileMeta compressedFileInfo,
            CancellationTokenSource cancellationTokenSource,
            CancellationToken cancellationToken,
            Action<string> writeLog)
        {
            try
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
            }
            catch (CompressDecompressFileException cdfExc)
            {
                writeLog($"Exception during decompressing: {cdfExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (IOException ioExc)
            {
                writeLog($"Exception during decompressing, " +
                    $"error during reading or writing file: {ioExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (UnauthorizedAccessException auExc)
            {
                writeLog($"Exception during decompressing , " +
                    $"error during reading or writing file: {auExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (Exception exc)
            {
                writeLog($"Exception during decompressing: {exc}");
                cancellationTokenSource.Cancel();
            }
        }

        private static void ConsumeFileBlocks(BlockingCollection<DecompressBlockData> queue,
            FileStream decompressedFileStream,
            object lockObject,
            CancellationTokenSource cancellationTokenSource,
            CancellationToken cancellationToken,
            Action<string> writeLog)
        {
            try
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
            }
            catch (CompressDecompressFileException cdfExc)
            {
                writeLog($"Exception during decompressing: {cdfExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (IOException ioExc)
            {
                writeLog($"Exception during decompressing, " +
                    $"error during reading or writing file: {ioExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (UnauthorizedAccessException auExc)
            {
                writeLog($"Exception during decompressing , " +
                    $"error during reading or writing file: {auExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (Exception exc)
            {
                writeLog($"Exception during decompressing: {exc}");
                cancellationTokenSource.Cancel();
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
