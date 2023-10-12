using System.Diagnostics;

namespace Veeam.FileSync.Services;

[DebuggerDisplay("F: {Hash} {RelativePath}")]
public record SyncFile(string Hash, string BasePath, string RelativePath)
{
    public string FullPath => Path.GetFullPath(Path.Join(BasePath, RelativePath));
}