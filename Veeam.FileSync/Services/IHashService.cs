namespace Veeam.FileSync.Services;

public interface IHashService
{
    Task<string> CalculateFileMD5Async(string filePath);
}