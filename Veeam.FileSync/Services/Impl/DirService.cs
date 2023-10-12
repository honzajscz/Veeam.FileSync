using Veeam.FileSync.Services;

namespace Veeam.FileSync;

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
        
        foreach (var task in filePaths.AsParallel().Select(async filePath => await CreateFileAsync(dirBasePath, filePath)))
        {
            yield return await task;
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