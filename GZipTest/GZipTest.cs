using GZipTest.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GZipTest
{
    class Program
    {
        private static readonly Dictionary<string, Func<string[], bool>> commandMap = new Dictionary<string, Func<string[], bool>>(StringComparer.InvariantCultureIgnoreCase)
        {
            [nameof(Compress)] = Compress,
            [nameof(Decompress)] = Decompress
        };

        private static readonly Dictionary<string, string> helpMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
        {
            [nameof(Compress)] = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} compress <file_name> <archive_name>",
            [nameof(Decompress)] = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} decompress <archive_name> <decompressed_file_name>"
        };

        static int Main(string[] args)
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionHandler);

            if (args.Length == 0)
            {
                Console.WriteLine("Invalid args");
                PrintHelp();
                return 1;
            }

            var command = args[0];

            if (!commandMap.ContainsKey(command))
            {
                Console.WriteLine("Invalid command");
                PrintHelp();
                return 1;
            }

            if (commandMap[command](args.Skip(1).ToArray()))
                return 0;
            else
                return 1;
        }

        private static void PrintHelp()
        {
            foreach (KeyValuePair<string, string> commandHelp in helpMap)
            {
                Console.WriteLine($"Example of usage {commandHelp.Key} operation: {commandHelp.Value}");
            }
        }

        static bool Compress(string[] args)
        {
            if (args.Length == 2)
            {
                var originalFileName = args[0];
                var archiveFileName = args[1];
                Console.WriteLine($"Compress of file {originalFileName} into {archiveFileName}");
                try
                {
                    bool success = new FileCompressor().Compress(originalFileName, archiveFileName, Console.WriteLine);
                    return success;
                }
                catch (CompressDecompressFileException compressFileExc)
                {
                    Console.WriteLine($"Exception during compressing {originalFileName} to {archiveFileName}: {compressFileExc.Message}");
                    return false;
                }
                catch (Exception exc)
                {
                    Console.WriteLine($"Exception during compressing {originalFileName} to {archiveFileName}: {exc}");
                    return false;
                }
            }
            else
            {
                Console.WriteLine($"Invalid args. Expected format: {helpMap[nameof(Compress)]}");
                return false;
            }
        }
        static bool Decompress(string[] args)
        {
            if (args.Length == 2)
            {
                var archiveFileName = args[0];
                var decompressingFileName = args[1];
                Console.WriteLine($"Decompress of file {archiveFileName} into {decompressingFileName}");
                try
                {
                    bool success = new FileDecompressor().Decompress(archiveFileName, decompressingFileName, Console.WriteLine);
                    return success;
                }
                catch (CompressDecompressFileException compressFileExc)
                {
                    Console.WriteLine($"Exception during decompressing {archiveFileName} to {decompressingFileName}: {compressFileExc.Message}");
                    return false;
                }
                catch (Exception exc)
                {
                    Console.WriteLine($"Exception during decompressing {archiveFileName} to {decompressingFileName}: {exc}");
                    return false;
                }
            }
            else
            {
                Console.WriteLine($"Invalid args. Expected format: {helpMap[nameof(Decompress)]}");
                return false;
            }
        }

        static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Console.WriteLine("Unhandled exception occuired: " + e.Message);
        }
    }
}
