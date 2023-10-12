using System.Security.Cryptography;

namespace Veeam.FileSync.Services.Impl;

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