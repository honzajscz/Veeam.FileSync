namespace Veeam.FileSync.Services;

public interface IDirService
{
    IEnumerable<SyncDir> GetDirectoriesRecursively(string dirBasePath);
    IAsyncEnumerable<SyncFile> GetFilesRecursivelyAsync(string dirFullPath);
}