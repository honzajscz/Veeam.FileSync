namespace Veeam.FileSync.Services;

public interface IDirService
{
    IEnumerable<SyncDir> GetDirectoriesRecursively(string dirBasePath);
    IAsyncEnumerable<SyncFile> GetFilesRecursivelyAsync(string dirFullPath);
    void CreateDir(string dirPath);
    void DeleteDir(string dirPath);
    void MoveFile(string sourceFilePath, string destinationFilePath);
    void DeleteFile(string filePath);
    void CopyFile(string sourceFilePath, string destinationFilePath);
}