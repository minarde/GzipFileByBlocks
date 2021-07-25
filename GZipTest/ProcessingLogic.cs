using GZipTest.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GZipTest
{
    class ProcessingLogic
    {
        public ProcessingResult Process<T>(Action<BlockingCollection<T>, CancellationToken> produce,
            Action<BlockingCollection<T>, CancellationToken> consume,
            Action<string> writeLog)
        {
            try
            {
                BlockingCollection<T> queue = new BlockingCollection<T>(Constants.QueueSize);

                var cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = cancellationTokenSource.Token;

                Task producer = Task.Factory.StartNew(() => ProduceFileBlocks<T>(
                    produce,
                    queue,
                    cancellationTokenSource,
                    cancellationToken,
                    writeLog));

                List<Task> consumers = new List<Task>();
                for (int i = 0; i < Constants.ConsumersCount; ++i)
                    consumers.Add(Task.Run(() => ConsumeFileBlocks<T>(
                        consume,
                        queue,
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
                        writeLog($"Processing was cancelled due to error");
                        return ProcessingResult.Fail();
                    }
                    else
                    {
                        return ProcessingResult.Successful();
                    }
                }
                catch (AggregateException aggrExc)
                {
                    foreach (Exception innerExc in aggrExc.InnerExceptions)
                    {
                        if (innerExc is CompressDecompressFileException)
                            writeLog($"Exception during processing: {innerExc.Message}");
                        else if (innerExc is IOException || innerExc is UnauthorizedAccessException)
                            writeLog($"Exception during processing, error during reading or writing file: {innerExc.Message}");
                        else
                            writeLog($"Exception during processing: {innerExc}");
                    }
                    return ProcessingResult.Fail();
                }
                catch (OperationCanceledException ocExc)
                {
                    writeLog($"Exception during processing, error: {ocExc.Message}");
                    return ProcessingResult.Fail();
                }
            }
            catch (UnauthorizedAccessException accessExc)
            {
                writeLog($"Exception during processing, error accessing file: {accessExc.Message}");
                return ProcessingResult.Fail();
            }
            catch (IOException ioExc)
            {
                writeLog($"Exception during processing, error: {ioExc.Message}");
                return ProcessingResult.Fail();
            }
        }

        private static void ProduceFileBlocks<T>(Action<BlockingCollection<T>, CancellationToken> produce,
            BlockingCollection<T> queue,
            CancellationTokenSource cancellationTokenSource,
            CancellationToken cancellationToken,
            Action<string> writeLog)
        {
            try
            {
                produce(queue, cancellationToken);
            }
            catch (CompressDecompressFileException cdfExc)
            {
                writeLog($"Exception during processing: {cdfExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (IOException ioExc)
            {
                writeLog($"Exception during processing, " +
                    $"error during reading or writing file: {ioExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (UnauthorizedAccessException auExc)
            {
                writeLog($"Exception during processing , " +
                    $"error during reading or writing file: {auExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (Exception exc)
            {
                writeLog($"Exception during processing: {exc}");
                cancellationTokenSource.Cancel();
            }
        }

        private static void ConsumeFileBlocks<T>(Action<BlockingCollection<T>, CancellationToken> consume,
            BlockingCollection<T> queue,
            CancellationTokenSource cancellationTokenSource,
            CancellationToken cancellationToken,
            Action<string> writeLog)
        {
            try
            {
                consume(queue, cancellationToken);
            }
            catch (CompressDecompressFileException cdfExc)
            {
                writeLog($"Exception during processing: {cdfExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (IOException ioExc)
            {
                writeLog($"Exception during processing, " +
                    $"error during reading or writing file: {ioExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (UnauthorizedAccessException auExc)
            {
                writeLog($"Exception during processing, " +
                    $"error during reading or writing file: {auExc.Message}");
                cancellationTokenSource.Cancel();
            }
            catch (Exception exc)
            {
                writeLog($"Exception during processing: {exc}");
                cancellationTokenSource.Cancel();
            }
        }
    }

    class ProcessingResult
    {
        public static ProcessingResult Successful()
        {
            return new ProcessingResult(true);
        }

        public static ProcessingResult Fail()
        {
            return new ProcessingResult(false);
        }

        private ProcessingResult(bool success)
        {
            this.Success = success;
        }

        public bool Success { get; private set; }
    }
}
