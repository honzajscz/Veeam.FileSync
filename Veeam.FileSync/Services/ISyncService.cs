namespace Veeam.FileSync.Services;

public interface ISyncService
{
    Task<SyncResult> SyncDirsAsync(string sourceDirBasePath, string replicaDirBasePath);
}