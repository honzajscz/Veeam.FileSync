namespace Veeam.FileSync.Services.Impl;

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

        var parallelQuery = filePaths.AsParallel().Select(async filePath => await InitSyncFileAsync(dirBasePath, filePath));
        foreach (var task in parallelQuery) 
            yield return await task;
    }
    
    public void CreateDir(string dirPath)
    {
        Directory.CreateDirectory(dirPath);
    }
    
    public void DeleteDir(string dirPath)
    {
        Directory.Delete(dirPath);
    }
    
    public void MoveFile(string sourceFilePath, string destinationFilePath)
    {
        File.Move(sourceFilePath, destinationFilePath);
    }
    
    public void DeleteFile(string filePath)
    {
        File.Delete(filePath);
    }
    
    public void CopyFile(string sourceFilePath, string destinationFilePath)
    {
        File.Copy(sourceFilePath, destinationFilePath);
    }

    private SyncDir CreateDir(string dirBasePath, string filePath)
    {
        var relativePath = Path.GetRelativePath(dirBasePath, filePath);
        var dir = new SyncDir(dirBasePath, relativePath);
        return dir;
    }

    private async Task<SyncFile> InitSyncFileAsync(string dirBasePath, string filePath)
    {
        var relativePath = Path.GetRelativePath(dirBasePath, filePath);
        var fileHash = await _hashService.CalculateFileMD5Async(filePath);
        var file = new SyncFile(fileHash, dirBasePath, relativePath);
        return file;
    }
}