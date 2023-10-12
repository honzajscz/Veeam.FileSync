using System.Diagnostics;
using log4net;
using log4net.Appender;
using log4net.Config;

using Veeam.FileSync.Services.Impl;


[assembly: XmlConfigurator(ConfigFile = "log4net.config")]

namespace Veeam.FileSync;

public class Program
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));

    public static async Task Main()
    {
        ConfigureLogPath(@"c:\temp\x.log");
        var sourceDirBasePath = @"c:\Temp\_archive\.azure\";
        var replicaDirBasePath = @"c:\Temp\_archive\.azure2\";
        using var cts = new CancellationTokenSource();
        var syncTask = RunSyncAsync(sourceDirBasePath, replicaDirBasePath, syncIntervalMs: 2000, retries: 5, cts.Token);
        Console.WriteLine("Press any key to quit");
        Console.ReadKey();
        Console.WriteLine("Waiting to complete current sync operation...");
        cts.Cancel();
        await syncTask;
    }

    private static async ValueTask RunSyncAsync(string sourceDirBasePath, string replicaDirBasePath, double syncIntervalMs, int retries, CancellationToken ct = default)
    {
        // Economy class DI. TODO: Use a properer DI container
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
                    Logger.Info("Legend: F is file, D is directory, = is matching, + is created, - is deleted, > is moved");
                }
                catch (Exception e)
                {
                    if (retries == 0)
                    {
                        Logger.Error($"The sync operation failed multiple times and it is therefore aborted.");
                        return;
                    }

                    Logger.Error($"The sync operation failed. {--retries} attempts left.");
                    Logger.Error(e.Message, e);
                }
            } while (await periodicTimer.WaitForNextTickAsync(ct));
        }
        catch (TaskCanceledException)
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