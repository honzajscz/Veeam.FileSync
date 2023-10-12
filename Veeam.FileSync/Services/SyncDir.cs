using System.Diagnostics;

namespace Veeam.FileSync.Services;

[DebuggerDisplay("D: {RelativePath}")]
public record SyncDir(string BasePath, string RelativePath)
{
    public string FullPath => Path.Join(BasePath, RelativePath);
}