using System.Diagnostics;
using CommandLine;
using CommandLine.Text;
using log4net;
using log4net.Appender;
using log4net.Config;
using Veeam.FileSync.Services.Impl;

[assembly: XmlConfigurator(ConfigFile = "log4net.config")]

namespace Veeam.FileSync;

public class Program
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));

    public static async Task Main(string[] args)
    {
        var parsedArgs = ParseArgs(args);

        ConfigureLogPath(parsedArgs.LogPath);
        using var cts = new CancellationTokenSource();
        var syncTask = RunSyncAsync(parsedArgs.SourceDirPath, parsedArgs.ReplicaDirPath, parsedArgs.SyncInterval, 5,
            cts.Token);
        Console.WriteLine("Press any key to quit");
        Console.ReadKey();
        Console.WriteLine("Waiting to complete current sync operation...");
        cts.Cancel();
        await syncTask;
    }

    private static Arguments ParseArgs(string[] args)
    {
        var parserResult = Parser.Default.ParseArguments<Arguments>(args);
        var errors = parserResult.Errors.ToArray();
        foreach (var error in errors)
        {
            var sentenceBuilder = SentenceBuilder.Create();
            Logger.Error(sentenceBuilder.FormatError(error)); // logging to the app folder!
        }

        if (parserResult.Errors.Any())
            Environment.Exit(-1);

        var options = parserResult.Value;
        return options;
    }

    private static async ValueTask RunSyncAsync(string sourceDirBasePath, string replicaDirBasePath,
        double syncIntervalMs, int retries, CancellationToken ct = default)
    {
        // Economy-class DI. TODO: Use a properer DI container
        var hashService = new HashService();
        var dirService = new DirService(hashService);
        var syncService = new SyncService(dirService);

        using var periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(syncIntervalMs));
        var watch = Stopwatch.StartNew();

        try
        {
            do
            {
                try
                {
                    Console.WriteLine();
                    Logger.Info($"Starting a sync operation from '{sourceDirBasePath}' > '{replicaDirBasePath}'");
                    watch.Reset();
                    watch.Start();

                    var syncRes = await syncService.SyncDirsAsync(sourceDirBasePath, replicaDirBasePath);
                    Logger.Info($"Completed the sync operation in {watch.Elapsed}:");
                    Logger.Info(
                        $"D=:{syncRes.MatchingDirs.Count()}\tD+:{syncRes.CreatedDirs.Count()}\tD-:{syncRes.DeletedDirs.Count()}");
                    Logger.Info(
                        $"F=:{syncRes.MatchingFiles.Count()}\tF+:{syncRes.CreatedFiles.Count()}\tF-:{syncRes.DeletedFiles.Count()}\tF>:{syncRes.MovedFiles.Count()}");
                    Logger.Info(
                        "Legend: F is file, D is directory, = is matching, + is created, - is deleted, > is moved");
                }
                catch (Exception e)
                {
                    if (retries == 0)
                    {
                        Logger.Error("The sync operation failed multiple times and it is therefore aborted.");
                        return;
                    }

                    Logger.Error($"The sync operation failed. {--retries} attempts left.");
                    Logger.Error(e.Message, e);
                }
            } while (await periodicTimer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void ConfigureLogPath(string logPath)
    {
        var fileAppender = (FileAppender)LogManager.GetRepository()
            .GetAppenders().First(appender => appender is FileAppender);
        fileAppender.File = logPath;
        fileAppender.ActivateOptions();
    }
}