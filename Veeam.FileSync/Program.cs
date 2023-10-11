using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Veeam.FileSync;

public class Program
{
    public static void Main()
    {
        //Directory.CreateDirectory(
        //    @);
        //File.Copy(@"c:\Temp\_archive\.azure\cliextensions\interactive\azext_interactive\tests\__init__.py", @"c:\Temp\_archive\.azure\cliextensions\interactive\azext_interactive\tests\__init__\x\__init__.py");

        var dirService = new DirService();
        var hashService = new HashService();
        var syncFileFactory = new SyncFile.Factory(hashService);
        var syncDirFactory = new SyncDir.Factory(dirService, hashService, syncFileFactory);

        var sourceDir = syncDirFactory.Create(@"c:\Temp\_archive\.azure\");
        var sourceSyncs = sourceDir.Flatten();
        var sourceFiles = sourceSyncs.OfType<SyncFile>().ToArray();
        var sourceFilesProcessed = new List<SyncFile>();

        var replicaDir = syncDirFactory.Create(@"c:\Temp\_archive\.azure2\");
        var replicaSyncs = replicaDir.Flatten();
        var replicaFiles = replicaSyncs.OfType<SyncFile>().ToList();

        // unchanged files
        foreach (var sourceFile in sourceFiles)
        {
            var identicalFileIndex = replicaFiles.FindIndex(rf => rf.Hash == sourceFile.Hash && rf.RelativePath == sourceFile.RelativePath);
            var identicalFileFound = -1 < identicalFileIndex;
            if (!identicalFileFound)
                continue;
            replicaFiles.RemoveAt(identicalFileIndex);
            sourceFilesProcessed.Add(sourceFile);
            //Console.WriteLine($"F= {sourceDir.RelativePath}");
        }

        // moved files
        sourceFiles = sourceFiles.Except(sourceFilesProcessed).ToArray();
        foreach (var sourceFile in sourceFiles)
        {
            var movedFileIndex = replicaFiles.FindIndex(rf => rf.Hash == sourceFile.Hash);
            var movedFileFound = -1 < movedFileIndex;
            if (!movedFileFound)
                continue;

            var movedFile = replicaFiles[movedFileIndex];
            var destFileName = Path.Combine(replicaDir.BasePath, sourceFile.RelativePath);
            var parentDir = Directory.GetParent(destFileName);
            if (!parentDir?.Exists ?? false) // TODO duplicated code 
            {
                Console.WriteLine($"D+ {Directory.GetParent(sourceFile.RelativePath)}");
                parentDir.Create();
            }
            File.Move(movedFile.FullPath, destFileName);

            replicaFiles.RemoveAt(movedFileIndex);
            sourceFilesProcessed.Add(sourceFile);
            Console.WriteLine($"F> {movedFile.RelativePath} to {sourceFile.RelativePath}");

        }

        // deleted files
        foreach (var replicaFile in replicaFiles)
        {
            File.Delete(replicaFile.FullPath);
            Console.WriteLine($"F- {replicaFile.RelativePath}");
        }

        // new files
        sourceFiles = sourceFiles.Except(sourceFilesProcessed).ToArray();
        foreach (var sourceFile in sourceFiles)
        {
            var destFileName = Path.Combine(replicaDir.BasePath, sourceFile.RelativePath);
            var parentDir = Directory.GetParent(destFileName);
            if (!parentDir?.Exists ?? false)
            {
                Console.WriteLine($"D+ {Directory.GetParent(sourceFile.RelativePath)}");
                parentDir.Create();
            }

            File.Copy(sourceFile.FullPath, destFileName);
            Console.WriteLine($"F+ {sourceFile.RelativePath}");
        }


        var sourceDirs = sourceSyncs.OfType<SyncDir>();
        var replicaDirs = replicaSyncs.OfType<SyncDir>();

        var emptySourceDirs = sourceDirs.ExceptBy(replicaDirs.Select(rd => rd.RelativePath), sd => sd.RelativePath).ToArray();
        foreach (var emptySourceDir in emptySourceDirs)
        {
            Directory.CreateDirectory(Path.Combine(replicaDir.BasePath, emptySourceDir.RelativePath));
            Console.WriteLine($"D+ {emptySourceDir.RelativePath}");
        }

        var emptyReplicaDirs = replicaDirs.ExceptBy(sourceDirs.Select(sd => sd.RelativePath), rd => rd.RelativePath).ToArray();
        foreach (var emptyReplicaDir in emptyReplicaDirs)
        {
            Directory.Delete(emptyReplicaDir.FullPath);
            Console.WriteLine($"D- {emptyReplicaDir.RelativePath}");
        }

    }
}

public interface ISync
{
    public string Hash { get; }
    public string FullPath { get; }
    public string BasePath { get; }
    public string RelativePath { get; }
    public SyncDir Parent { get; }
}

[DebuggerDisplay("D {Hash} {RelativePath}")]
public class SyncDir : ISync
{
    public IEnumerable<ISync> Children { get; private set; }
    public string BasePath { get; private set; }
    public string RelativePath { get; private set; }
    public string Hash { get; private set; }
    public string FullPath => Path.Join(BasePath, RelativePath);
    public SyncDir Parent { get; set; }
    public bool IsRoot => Parent != null;

    public IEnumerable<ISync> Flatten()
    {
        var list = new List<ISync> { this };
        Flatten(list);
        return list;
    }

    private void Flatten(IList<ISync> list)
    {
        foreach (var child in Children)
        {
            list.Add(child);
            if (child is SyncDir Dir) Dir.Flatten(list);
        }
    }

    public class Factory : ISyncFactory<SyncDir>
    {
        private readonly IDirService _dirService;
        private readonly IHashService _hashService;
        private readonly ISyncFactory<SyncFile> _syncFileFactory;

        public Factory(IDirService dirService, IHashService hashService, ISyncFactory<SyncFile> syncFileFactory)
        {
            _dirService = dirService;
            _hashService = hashService;
            _syncFileFactory = syncFileFactory;
        }

        public SyncDir Create(string basePath)
        {
            return Create(basePath, string.Empty, null!);
        }

        public SyncDir Create(string basePath, string relativePath, SyncDir parent)
        {
            var children = new List<ISync>();
            var syncDir = new SyncDir
            { BasePath = basePath, RelativePath = relativePath, Children = children, Parent = parent };

            var directories = _dirService.GetDirectories(syncDir.FullPath);
            foreach (var directory in directories)
            {
                var childDir = Create(basePath, Path.GetRelativePath(basePath, directory), syncDir);
                children.Add(childDir);
            }

            var files = _dirService.GetFiles(syncDir.FullPath);
            foreach (var file in files)
            {
                var syncFile = _syncFileFactory.Create(basePath, Path.GetRelativePath(basePath, file), syncDir);
                children.Add(syncFile);
            }

            var dirHash = _hashService.CalculateMD5(syncDir);
            syncDir.Hash = dirHash;
            return syncDir;
        }
    }
}

public interface ISyncFactory<T> where T : ISync
{
    T Create(string basePath, string relativePath, SyncDir parentDir);
}



public interface IHashService
{
    string CalculateMD5(SyncDir syncDir);
    string CalculateMD5(SyncFile syncFile);
}

internal class HashService : IHashService
{
    public string CalculateMD5(SyncDir syncDir)
    {
        //var creationBytes = BitConverter.GetBytes(File.GetCreationTimeUtc(syncDir.FullPath).Ticks);
        var lastWriteBytes = BitConverter.GetBytes(File.GetLastWriteTimeUtc(syncDir.FullPath).Ticks);

        var childrenHashes = new StringBuilder();
        foreach (var child in syncDir.Children.OrderBy(child => child.Hash)) childrenHashes.AppendLine(child.Hash);
        var childrenBytes = Encoding.ASCII.GetBytes(childrenHashes.ToString());
        var bytesToHash = childrenBytes/*.Union(creationBytes)*/.Union(lastWriteBytes).ToArray();

        using var md5 = MD5.Create();

        var hash = md5.ComputeHash(bytesToHash);

        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        ;
    }


    public string CalculateMD5(SyncFile syncFile)
    {
        using var md5 = MD5.Create();
        var fileBytes = File.ReadAllBytes(syncFile.FullPath);
        //var creationBytes = BitConverter.GetBytes(File.GetCreationTimeUtc(syncFile.FullPath).Ticks);
        var lastWriteBytes = BitConverter.GetBytes(File.GetLastWriteTimeUtc(syncFile.FullPath).Ticks);
        var bytesToHash = fileBytes/*.Union(creationBytes)*/.Union(lastWriteBytes).ToArray();
        var hash = md5.ComputeHash(bytesToHash);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}

public interface IDirService
{
    IEnumerable<string> GetDirectories(string dirFullPath);
    IEnumerable<string> GetFiles(string dirFullPath);
}

public class DirService : IDirService
{
    public IEnumerable<string> GetDirectories(string dirFullPath)
    {
        return Directory.Exists(dirFullPath) ? Directory.GetDirectories(dirFullPath) : Enumerable.Empty<string>();
    }

    public IEnumerable<string> GetFiles(string dirFullPath)
    {
        return Directory.Exists(dirFullPath) ? Directory.GetFiles(dirFullPath) : Enumerable.Empty<string>();
    }
}

[DebuggerDisplay("F: {Hash} {RelativePath}")]
public class SyncFile : ISync
{
    public string Hash { get; private set; }

    public string FullPath => Path.Join(BasePath, RelativePath);

    public string BasePath { get; private set; }

    public string RelativePath { get; private set; }

    public SyncDir Parent { get; private set; }

    public class Factory : ISyncFactory<SyncFile>
    {
        private readonly IHashService _hashService;

        public Factory(IHashService hashService)
        {
            _hashService = hashService;
        }

        public SyncFile Create(string basePath, string relativePath, SyncDir parent)
        {
            var file = new SyncFile { Parent = parent, BasePath = basePath, RelativePath = relativePath };
            file.Hash = _hashService.CalculateMD5(file);
            return file;
        }
    }
}