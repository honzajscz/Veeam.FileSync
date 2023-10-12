namespace Veeam.FileSync.Services;

public record SyncResult(IEnumerable<SyncDir> MatchingDirs, IEnumerable<SyncDir> CreatedDirs,
    IEnumerable<SyncDir> DeletedDirs, IEnumerable<SyncFile> MatchingFiles, IEnumerable<SyncFile> CreatedFiles,
    IEnumerable<SyncFile> DeletedFiles, IEnumerable<SyncFile> MovedFiles);