using log4net;

namespace Veeam.FileSync.Services.Impl;

public class SyncService : ISyncService
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

        return new SyncResult(matchingDirs, createdDirs, deletedDirs, matchingFiles, createdFiles, deletedFiles,
            movedFiles);
    }

    private SyncDir[] CreateDirs(IEnumerable<SyncDir> sourceDirs, IEnumerable<SyncDir> replicaDirs,
        string replicaDirBasePath)
    {
        var createdDirs = sourceDirs.ExceptBy(replicaDirs.Select(rd => rd.RelativePath), sd => sd.RelativePath)
            .ToArray();

        foreach (var createdDir in createdDirs)
        {
            _dirService.CreateDir(Path.Combine(replicaDirBasePath, createdDir.RelativePath));
            Logger.Info($"D+ {createdDir.RelativePath}");
        }

        return createdDirs;
    }

    private SyncDir[] FindMatchingDirs(IEnumerable<SyncDir> sourceDirs, IEnumerable<SyncDir> replicaDirs)
    {
        var matchingDirs = sourceDirs.IntersectBy(replicaDirs.Select(rd => rd.RelativePath), sd => sd.RelativePath)
            .ToArray();
        foreach (var matchingDir in matchingDirs) Logger.Debug($"D= {matchingDir.RelativePath}");

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
            _dirService.MoveFile(movedFile.FullPath, destFileName);

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
            _dirService.DeleteFile(replicaFile.FullPath);
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
            _dirService.CopyFile(sourceFile.FullPath, destFileName);
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
            _dirService.DeleteDir(deletedDir.FullPath);
            Logger.Info($"D- '{deletedDir.RelativePath}'");
        }

        return deletedDirs;
    }
}