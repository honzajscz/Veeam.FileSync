using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using log4net;
using log4net.Appender;
using log4net.Config;
using Veeam.FileSync;


[assembly: XmlConfigurator(ConfigFile = "log4net.config")]

namespace Veeam.FileSync;

public class Program
{
    private static ILog Logger = LogManager.GetLogger(typeof(Program));

    public static async Task Main()
    {
        ConfigureLogPath(@"c:\temp\x.log");
        var sourceDirBasePath = @"c:\Temp\_archive\.azure\";
        var replicaDirBasePath = @"c:\Temp\_archive\.azure2\";
        using var cts = new CancellationTokenSource();
        var syncTask = DoSyncAsync(sourceDirBasePath, replicaDirBasePath, syncIntervalMs: 2000, retries: 5, cts.Token);
        Console.WriteLine("Press any key to quit");
        Console.ReadKey();
        Console.WriteLine("Waiting to complete current sync operation");
        cts.Cancel();
        await syncTask;
    }

    private static async ValueTask DoSyncAsync(string sourceDirBasePath, string replicaDirBasePath, double syncIntervalMs, int retries, CancellationToken ct = default)
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

                    Logger.Error($"The sync operation failed. {--retries} attempts left");
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

public record SyncResult(IEnumerable<SyncDir> MatchingDirs, IEnumerable<SyncDir> CreatedDirs, IEnumerable<SyncDir> DeletedDirs, IEnumerable<SyncFile> MatchingFiles, IEnumerable<SyncFile> CreatedFiles, IEnumerable<SyncFile> DeletedFiles, IEnumerable<SyncFile> MovedFiles) { }

public class SyncService
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(SyncService));
    private readonly IDirService _dirService;

    public SyncService(IDirService dirService)
    {
        _dirService = dirService;
    }

    public async Task<SyncResult> SyncDirsAsync(string sourceDirBasePath, string replicaDirBasePath)
    {
        var sourceDirs = _dirService.GetDirectoriesRecursively(sourceDirBasePath);
        var sourceFiles = await _dirService.GetFilesRecursivelyAsync(sourceDirBasePath).ToListAsync();
        var replicaDirs = _dirService.GetDirectoriesRecursively(replicaDirBasePath);
        var replicaFiles = await _dirService.GetFilesRecursivelyAsync(replicaDirBasePath).ToListAsync();

        var createdDirs = CreateDirs(sourceDirs, replicaDirs, replicaDirBasePath);
        var matchingDirs = FindMatchingDirs(sourceDirs, replicaDirs);

        var matchingFiles = FindMatchingFiles(sourceFiles, replicaFiles);
        var (createdFiles, movedFiles) = MoveFiles(sourceFiles, replicaFiles, replicaDirBasePath);
        var deletedFiles = DeleteFiles(replicaFiles);
        CreateFiles(createdFiles, replicaDirBasePath);

        var deletedDirs = DeleteDirs(sourceDirs, replicaDirs);

        return new SyncResult(matchingDirs, createdDirs, deletedDirs, matchingFiles, createdFiles, deletedFiles, movedFiles);
    }

    private SyncDir[] CreateDirs(IEnumerable<SyncDir> sourceDirs, IEnumerable<SyncDir> replicaDirs,
        string replicaDirBasePath)
    {
        var createdDirs = sourceDirs.ExceptBy(replicaDirs.Select(rd => rd.RelativePath), sd => sd.RelativePath)
            .ToArray();

        foreach (var createdDir in createdDirs)
        {
            Directory.CreateDirectory(Path.Combine(replicaDirBasePath, createdDir.RelativePath));
            Logger.Info($"D+ {createdDir.RelativePath}");
        }

        return createdDirs;
    }

    private SyncDir[] FindMatchingDirs(IEnumerable<SyncDir> sourceDirs, IEnumerable<SyncDir> replicaDirs)
    {
        var matchingDirs = sourceDirs.IntersectBy(replicaDirs.Select(rd => rd.RelativePath), sd => sd.RelativePath).ToArray();
        foreach (var matchingDir in matchingDirs)
        {
            Logger.Debug($"D= {matchingDir.RelativePath}");
        }

        return matchingDirs;
    }

    private List<SyncFile> FindMatchingFiles(List<SyncFile> sourceFiles, List<SyncFile> replicaFiles)
    {
        var movedFiles = new List<SyncFile>();
        foreach (var sourceFile in sourceFiles)
        {
            var matchingFileIndex =
                replicaFiles.FindIndex(rf => rf.Hash == sourceFile.Hash && rf.RelativePath == sourceFile.RelativePath);
            var matchingFileFound = -1 < matchingFileIndex;
            if (!matchingFileFound)
                continue;

            replicaFiles.RemoveAt(matchingFileIndex);
            movedFiles.Add(sourceFile);
            Logger.Debug($"F= '{sourceFile.RelativePath}'");
        }

        movedFiles.ForEach(mf => sourceFiles.Remove(mf));
        return movedFiles;
    }

    private (List<SyncFile> createdFiles, List<SyncFile> movedFiles) MoveFiles(List<SyncFile> sourceFiles,
        List<SyncFile> replicaFiles, string replicaDirBasePath)
    {
        var movedFiles = new List<SyncFile>();
        var createdFiles = new List<SyncFile>();
        foreach (var sourceFile in sourceFiles)
        {
            var movedFileIndex = replicaFiles.FindIndex(rf => rf.Hash == sourceFile.Hash);
            var movedFileFound = -1 < movedFileIndex;
            if (!movedFileFound)
            {
                createdFiles.Add(sourceFile);
                continue;
            }

            var movedFile = replicaFiles[movedFileIndex];
            var destFileName = Path.Combine(replicaDirBasePath, sourceFile.RelativePath);
            File.Move(movedFile.FullPath, destFileName);

            replicaFiles.RemoveAt(movedFileIndex);
            movedFiles.Add(sourceFile);
            Logger.Info($"F> '{movedFile.RelativePath}' > '{sourceFile.RelativePath}'");
        }

        return (createdFiles, movedFiles);
    }

    private List<SyncFile> DeleteFiles(List<SyncFile> replicaFiles)
    {
        var deletedFiles = new List<SyncFile>();

        foreach (var replicaFile in replicaFiles)
        {
            File.Delete(replicaFile.FullPath);
            deletedFiles.Add(replicaFile);
            Logger.Info($"F- '{replicaFile.RelativePath}'");
        }

        return deletedFiles;
    }

    private void CreateFiles(List<SyncFile> createdFiles, string replicaDirBasePath)
    {
        foreach (var sourceFile in createdFiles)
        {
            var destFileName = Path.Combine(replicaDirBasePath, sourceFile.RelativePath);
            File.Copy(sourceFile.FullPath, destFileName);
            Logger.Info($"F+ '{sourceFile.RelativePath}'");
        }
    }

    private SyncDir[] DeleteDirs(IEnumerable<SyncDir> sourceDirs, IEnumerable<SyncDir> replicaDirs)
    {
        var deletedDirs = replicaDirs
            .ExceptBy(sourceDirs.Select(sd => sd.RelativePath), rd => rd.RelativePath)
            .OrderByDescending(repDir => repDir.FullPath.Length)
            .ToArray();
        foreach (var deletedDir in deletedDirs)
        {
            Directory.Delete(deletedDir.FullPath);
            Logger.Info($"D- '{deletedDir.RelativePath}'");
        }

        return deletedDirs;
    }
}

[DebuggerDisplay("F: {Hash} {RelativePath}")]
public record SyncFile(string Hash, string BasePath, string RelativePath)
{
    public string FullPath => Path.GetFullPath(Path.Join(BasePath, RelativePath));
}

[DebuggerDisplay("D: {RelativePath}")]
public record SyncDir(string BasePath, string RelativePath)
{
    public string FullPath => Path.Join(BasePath, RelativePath);
}

public interface IHashService
{
    Task<string> CalculateFileMD5Async(string filePath);
}

public interface IDirService
{
    IEnumerable<SyncDir> GetDirectoriesRecursively(string dirBasePath);
    IAsyncEnumerable<SyncFile> GetFilesRecursivelyAsync(string dirFullPath);
}

public class HashService : IHashService
{
    public async Task<string> CalculateFileMD5Async(string filePath)
    {
        using var md5 = MD5.Create();
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var lastWriteBytes = BitConverter.GetBytes(File.GetLastWriteTimeUtc(filePath).Ticks);
        var bytesToHash = fileBytes.Union(lastWriteBytes).ToArray();
        var hash = md5.ComputeHash(bytesToHash);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}

public class DirService : IDirService
{
    private readonly IHashService _hashService;

    public DirService(IHashService hashService)
    {
        _hashService = hashService;
    }

    public IEnumerable<SyncDir> GetDirectoriesRecursively(string dirBasePath)
    {
        dirBasePath = Path.GetFullPath(dirBasePath);

        var dirPaths = Directory.Exists(dirBasePath)
            ? Directory.EnumerateDirectories(dirBasePath, "*", SearchOption.AllDirectories)
            : Enumerable.Empty<string>();
        return dirPaths.Select(dirPath => CreateDir(dirBasePath, dirPath));
    }

    public async IAsyncEnumerable<SyncFile> GetFilesRecursivelyAsync(string dirBasePath)
    {
        dirBasePath = Path.GetFullPath(dirBasePath);
        var filePaths = Directory.Exists(dirBasePath)
            ? Directory.EnumerateFiles(dirBasePath, "*", SearchOption.AllDirectories)
            : Enumerable.Empty<string>();
        foreach (var filePath in filePaths)
        {
            var file = await CreateFileAsync(dirBasePath, filePath);
            yield return file;
        }
    }

    private SyncDir CreateDir(string dirBasePath, string filePath)
    {
        var relativePath = Path.GetRelativePath(dirBasePath, filePath);
        var dir = new SyncDir(dirBasePath, relativePath);
        return dir;
    }

    private async Task<SyncFile> CreateFileAsync(string dirBasePath, string filePath)
    {
        var relativePath = Path.GetRelativePath(dirBasePath, filePath);
        var fileHash = await _hashService.CalculateFileMD5Async(filePath);
        var file = new SyncFile(fileHash, dirBasePath, relativePath);
        return file;
    }
}