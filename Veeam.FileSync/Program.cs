using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Veeam.FileSync
{
    public class Program
    {
        public static void Main()
        {
            //Directory.CreateDirectory(
            //    @"c:\Temp\_archive\.azure\cliextensions\interactive\azext_interactive\tests\__init__\x");
            //File.Copy(@"c:\Temp\_archive\.azure\cliextensions\interactive\azext_interactive\tests\__init__.py", @"c:\Temp\_archive\.azure\cliextensions\interactive\azext_interactive\tests\__init__\x\__init__.py");

            var source = SyncDirFactory.Create(@"c:\Temp\_archive\.azure\", "", null);
            var replica= SyncDirFactory.Create(@"c:\Temp\_archive\.azure2\", "", null);
            
        }
    }

    interface ISync
    {
        public string Hash { get; }
        public string FullPath { get; }
        public string BasePath { get; }
        public string RelativePath { get; }
        public SyncDir Parent { get; }
    }



    [DebuggerDisplay("Dir: {RelativePath} {Hash}")]
    class SyncDir : ISync
    {
        /// <inheritdoc />
        public string BasePath { get; set; }

        /// <inheritdoc />
        public string RelativePath { get; set; }
        public string Hash { get; set; }

        /// <inheritdoc />
        public string FullPath => Path.Join(BasePath, RelativePath);
        public IEnumerable<ISync> Children { get; set; }
        public SyncDir Parent { get; set; }

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
                if (child is SyncDir Dir)
                {
                    Dir.Flatten(list);
                }
            }
        }

    }

    class SyncDirFactory
    {
        public static SyncDir Create(string basePath, string relativePath, SyncDir parent)
        {
            
            var children = new List<ISync>();
            var syncDir = new SyncDir() { BasePath = basePath, RelativePath = relativePath, Children = children, Parent = parent,};

            var directories = Directory.GetDirectories(syncDir.FullPath);
            foreach (var directory in directories)
            {
                var childDir = Create(basePath, Path.GetRelativePath(basePath, directory), syncDir);
                children.Add(childDir);
            }

            var files = Directory.GetFiles(syncDir.FullPath);
            foreach (var file in files)
            {
                var syncFile = SyncFileFactory.Create(basePath, Path.GetRelativePath(basePath, file), syncDir);
                children.Add(syncFile);
            }

            var childrenHashes = new StringBuilder();
            foreach (var child in children.OrderBy(child => child.Hash))
            {
                childrenHashes.AppendLine(child.Hash);
            }

            var dirHash = CalculateMD5(childrenHashes.ToString(), syncDir.FullPath);
            syncDir.Hash = dirHash;
            return syncDir;
        }

        static string CalculateMD5(string childrenHashes, string dirPath)
        {
            var creationBytes = BitConverter.GetBytes(File.GetCreationTimeUtc(dirPath).Ticks);
            var lastWriteBytes =  BitConverter.GetBytes(File.GetLastWriteTimeUtc(dirPath).Ticks);
            byte[] childrenBytes = Encoding.ASCII.GetBytes(childrenHashes);
            var bytesToHash = childrenBytes.Union(creationBytes).Union(lastWriteBytes).ToArray();

            using var md5 = MD5.Create();

            var hash = md5.ComputeHash(bytesToHash);
            
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(); ;
        }
    }


    [DebuggerDisplay("File: {RelativePath} {Hash}")]
    internal class SyncFile : ISync
    {
        public string Hash { get; set; }

        public string FullPath => Path.Join(BasePath, RelativePath);

        public string BasePath { get; set; }

        public string RelativePath { get; set; }

        public SyncDir Parent { get; set; }
    }

    class SyncFileFactory
    {
        public static SyncFile Create(string basePath, string relativePath, SyncDir parent)
        {
            var file = new SyncFile() {Parent = parent, BasePath = basePath, RelativePath = relativePath };
            file.Hash = CalculateMD5(file.FullPath);
            return file;
        }

        static string CalculateMD5(string filename)
        {
            using var md5 = MD5.Create();
            var fileBytes = File.ReadAllBytes(filename);
            var creationBytes = BitConverter.GetBytes(File.GetCreationTimeUtc(filename).Ticks);
            var lastWriteBytes =  BitConverter.GetBytes(File.GetLastWriteTimeUtc(filename).Ticks);
            var bytesToHash = fileBytes.Union(creationBytes).Union(lastWriteBytes).ToArray();
            var hash = md5.ComputeHash(bytesToHash);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}






