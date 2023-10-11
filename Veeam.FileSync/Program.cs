using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace Veeam.FileSync;

public class Program
{
    public static async Task Main()
    {
        //Directory.CreateDirectory(
        //    @);
        //File.Copy(@"c:\Temp\_archive\.azure\cliextensions\interactive\azext_interactive\tests\__init__.py", @"c:\Temp\_archive\.azure\cliextensions\interactive\azext_interactive\tests\__init__\x\__init__.py");

        var hashService = new HashService();
        var dirService = new DirService(hashService);

        var sourceDirBasePath = @"c:\Temp\_archive\.azure\";
        var replicaDirBasePath = @"c:\Temp\_archive\.azure2\";

        
        var fileYetToProcess = new List<SyncFile>();
        var unchangedFiles = new List<SyncFile>();
        
        var movedFiles = new List<SyncFile>();
        var createdFiles = new List<SyncFile>();
        var deletedFiles = new List<SyncFile>();


        // create missing replica dirs
        var sourceDirs = dirService.GetDirectoriesRecursively(sourceDirBasePath);
        var replicaDirs = dirService.GetDirectoriesRecursively(replicaDirBasePath);
        var unchangedDirs = sourceDirs.IntersectBy(replicaDirs.Select(rd => rd.RelativePath), sd => sd.RelativePath).ToArray();
        var createdDirs = sourceDirs.ExceptBy(replicaDirs.Select(rd => rd.RelativePath), sd => sd.RelativePath).ToArray();
        foreach (var createdDir in createdDirs)
        {
            Directory.CreateDirectory(Path.Combine(replicaDirBasePath, createdDir.RelativePath));
            Console.WriteLine($"D+ {createdDir.RelativePath}");
        }
        
        var sourceFiles = dirService.GetFilesRecursivelyAsync(sourceDirBasePath);
        var replicaFiles = await dirService.GetFilesRecursivelyAsync(replicaDirBasePath).ToListAsync();
        
        // identical files
        await foreach (var sourceFile in sourceFiles)
        {
            var identicalFileIndex = replicaFiles.FindIndex(rf => rf.Hash == sourceFile.Hash && rf.RelativePath == sourceFile.RelativePath);
            var identicalFileFound = -1 < identicalFileIndex;
            if (!identicalFileFound)
            {
                fileYetToProcess.Add(sourceFile);
                continue;
            }
            replicaFiles.RemoveAt(identicalFileIndex);
            unchangedFiles.Add(sourceFile);
            //Console.WriteLine($"F= {sourceFile.RelativePath}");
        }

        // moved files
        foreach (var sourceFile in fileYetToProcess)
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
            Console.WriteLine($"F> {movedFile.RelativePath} to {sourceFile.RelativePath}");
        }

        // deleted files
        foreach (var replicaFile in replicaFiles)
        {
            File.Delete(replicaFile.FullPath);
            deletedFiles.Add(replicaFile);
            Console.WriteLine($"F- {replicaFile.RelativePath}");
        }

        // created files
        foreach (var sourceFile in createdFiles)
        {
            var destFileName = Path.Combine(replicaDirBasePath, sourceFile.RelativePath);
            File.Copy(sourceFile.FullPath, destFileName);
            Console.WriteLine($"F+ {sourceFile.RelativePath}");
        }
        
        // delete left-over replica dirs
        var deletedDirs = replicaDirs.ExceptBy(sourceDirs.Select(sd => sd.RelativePath), rd => rd.RelativePath).ToArray();
        foreach (var deletedDir in deletedDirs)
        {
            Directory.Delete(deletedDir.FullPath);
            Console.WriteLine($"D- {deletedDir.RelativePath}");
        }

        Console.WriteLine($"D=:{unchangedDirs.Count()}\tD+:{createdDirs.Count()}\tD-:{deletedDirs.Count()}");
        Console.WriteLine($"F=:{unchangedFiles.Count()}\tF+:{createdFiles.Count()}\tF-:{deletedFiles.Count()}\tF>:{movedFiles.Count()}");
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

        var dirPaths = Directory.Exists(dirBasePath) ? Directory.EnumerateDirectories(dirBasePath, "*", SearchOption.AllDirectories) : Enumerable.Empty<string>();
        return dirPaths.Select(dirPath => CreateDir(dirBasePath, dirPath));
    }

    public async IAsyncEnumerable<SyncFile> GetFilesRecursivelyAsync(string dirBasePath)
    {
        dirBasePath = Path.GetFullPath(dirBasePath);
        var filePaths = Directory.Exists(dirBasePath) ? Directory.EnumerateFiles(dirBasePath, "*", SearchOption.AllDirectories) : Enumerable.Empty<string>();
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