using Moq;
using NUnit.Framework;
using Veeam.FileSync.Services;
using Veeam.FileSync.Services.Impl;

namespace Veeam.FileSync.Tests;

public class SyncServiceTests
{
    [Test]
    public void Matching_file()
    {
        // arrange
        var dirService = Mock.Of<IDirService>(
            p =>
                p.GetFilesRecursivelyAsync("Y:\\source") ==
                new[] { new SyncFile("source101Hash", "Y:\\source", "01") }.ToAsyncEnumerable() &&
                p.GetFilesRecursivelyAsync("Y:\\replica") ==
                new[] { new SyncFile("source101Hash", "Y:\\replica", "01") }.ToAsyncEnumerable()
        );

        // act
        var syncService = new SyncService(dirService);
        var syncResult = syncService.SyncDirsAsync("Y:\\source", "Y:\\replica").Result;

        // assert 
        Assert.AreEqual(0, syncResult.MatchingDirs.Count());
        Assert.AreEqual(0, syncResult.CreatedDirs.Count());
        Assert.AreEqual(0, syncResult.DeletedDirs.Count());

        Assert.AreEqual(1, syncResult.MatchingFiles.Count());
        Assert.AreEqual(0, syncResult.CreatedFiles.Count());
        Assert.AreEqual(0, syncResult.DeletedFiles.Count());
        Assert.AreEqual(0, syncResult.MovedFiles.Count());
    }

    [Test]
    public void Moved_file()
    {
        // arrange
        var dirService = Mock.Of<IDirService>(
            p =>
                p.GetFilesRecursivelyAsync("Y:\\source") ==
                new[] { new SyncFile("source101Hash", "Y:\\source", "01") }.ToAsyncEnumerable() &&
                p.GetFilesRecursivelyAsync("Y:\\replica") ==
                new[] { new SyncFile("source101Hash", "Y:\\replica", "02") }.ToAsyncEnumerable()
        );

        // act
        var syncService = new SyncService(dirService);
        var syncResult = syncService.SyncDirsAsync("Y:\\source", "Y:\\replica").Result;

        // assert 
        Assert.AreEqual(0, syncResult.MatchingDirs.Count());
        Assert.AreEqual(0, syncResult.CreatedDirs.Count());
        Assert.AreEqual(0, syncResult.DeletedDirs.Count());

        Assert.AreEqual(0, syncResult.MatchingFiles.Count());
        Assert.AreEqual(0, syncResult.CreatedFiles.Count());
        Assert.AreEqual(0, syncResult.DeletedFiles.Count());
        Assert.AreEqual(1, syncResult.MovedFiles.Count());

        Mock.Get(dirService).Verify(service => service.MoveFile("Y:\\replica\\02", "Y:\\replica\\01"));
    }

    [Test]
    public void Delete_file()
    {
        // arrange
        var dirService = Mock.Of<IDirService>(
            p =>
                p.GetFilesRecursivelyAsync("Y:\\source") == Array.Empty<SyncFile>().ToAsyncEnumerable() &&
                p.GetFilesRecursivelyAsync("Y:\\replica") ==
                new[] { new SyncFile("source101Hash", "Y:\\replica", "02") }.ToAsyncEnumerable()
        );

        // act
        var syncService = new SyncService(dirService);
        var syncResult = syncService.SyncDirsAsync("Y:\\source", "Y:\\replica").Result;

        // assert 
        Assert.AreEqual(0, syncResult.MatchingDirs.Count());
        Assert.AreEqual(0, syncResult.CreatedDirs.Count());
        Assert.AreEqual(0, syncResult.DeletedDirs.Count());

        Assert.AreEqual(0, syncResult.MatchingFiles.Count());
        Assert.AreEqual(0, syncResult.CreatedFiles.Count());
        Assert.AreEqual(1, syncResult.DeletedFiles.Count());
        Assert.AreEqual(0, syncResult.MovedFiles.Count());

        Mock.Get(dirService).Verify(service => service.DeleteFile("Y:\\replica\\02"));
    }

    [Test]
    public void Create_file()
    {
        // arrange
        var dirService = Mock.Of<IDirService>(
            p =>
                p.GetFilesRecursivelyAsync("Y:\\source") ==
                new[] { new SyncFile("source101Hash", "Y:\\source", "01") }.ToAsyncEnumerable() &&
                p.GetFilesRecursivelyAsync("Y:\\replica") == Array.Empty<SyncFile>().ToAsyncEnumerable()
        );

        // act
        var syncService = new SyncService(dirService);
        var syncResult = syncService.SyncDirsAsync("Y:\\source", "Y:\\replica").Result;

        // assert 
        Assert.AreEqual(0, syncResult.MatchingDirs.Count());
        Assert.AreEqual(0, syncResult.CreatedDirs.Count());
        Assert.AreEqual(0, syncResult.DeletedDirs.Count());

        Assert.AreEqual(0, syncResult.MatchingFiles.Count());
        Assert.AreEqual(1, syncResult.CreatedFiles.Count());
        Assert.AreEqual(0, syncResult.DeletedFiles.Count());
        Assert.AreEqual(0, syncResult.MovedFiles.Count());

        Mock.Get(dirService).Verify(service => service.CopyFile("Y:\\source\\01", "Y:\\replica\\01"));
    }

    [Test]
    public void Matching_dir()
    {
        // arrange
        var dirService = Mock.Of<IDirService>(
            p =>
                p.GetFilesRecursivelyAsync("Y:\\source") == Array.Empty<SyncFile>().ToAsyncEnumerable() &&
                p.GetFilesRecursivelyAsync("Y:\\replica") == Array.Empty<SyncFile>().ToAsyncEnumerable() &&
                p.GetDirectoriesRecursively("Y:\\source") ==
                new[] { new SyncDir("Y:\\source", "01") }.ToAsyncEnumerable() &&
                p.GetDirectoriesRecursively("Y:\\replica") ==
                new[] { new SyncDir("Y:\\replica", "01") }.ToAsyncEnumerable()
        );

        // act
        var syncService = new SyncService(dirService);
        var syncResult = syncService.SyncDirsAsync("Y:\\source", "Y:\\replica").Result;

        // assert 
        Assert.AreEqual(1, syncResult.MatchingDirs.Count());
        Assert.AreEqual(0, syncResult.CreatedDirs.Count());
        Assert.AreEqual(0, syncResult.DeletedDirs.Count());

        Assert.AreEqual(0, syncResult.MatchingFiles.Count());
        Assert.AreEqual(0, syncResult.CreatedFiles.Count());
        Assert.AreEqual(0, syncResult.DeletedFiles.Count());
        Assert.AreEqual(0, syncResult.MovedFiles.Count());
    }

    [Test]
    public void Create_dir()
    {
        // arrange
        var dirService = Mock.Of<IDirService>(
            p =>
                p.GetFilesRecursivelyAsync("Y:\\source") == Array.Empty<SyncFile>().ToAsyncEnumerable() &&
                p.GetFilesRecursivelyAsync("Y:\\replica") == Array.Empty<SyncFile>().ToAsyncEnumerable() &&
                p.GetDirectoriesRecursively("Y:\\source") ==
                new[] { new SyncDir("Y:\\source", "01") }.ToAsyncEnumerable()
        );

        // act
        var syncService = new SyncService(dirService);
        var syncResult = syncService.SyncDirsAsync("Y:\\source", "Y:\\replica").Result;

        // assert 
        Assert.AreEqual(0, syncResult.MatchingDirs.Count());
        Assert.AreEqual(1, syncResult.CreatedDirs.Count());
        Assert.AreEqual(0, syncResult.DeletedDirs.Count());

        Assert.AreEqual(0, syncResult.MatchingFiles.Count());
        Assert.AreEqual(0, syncResult.CreatedFiles.Count());
        Assert.AreEqual(0, syncResult.DeletedFiles.Count());
        Assert.AreEqual(0, syncResult.MovedFiles.Count());

        Mock.Get(dirService).Verify(service => service.CreateDir("Y:\\replica\\01"));
    }

    [Test]
    public void Deleted_dir()
    {
        // arrange
        var dirService = Mock.Of<IDirService>(
            p =>
                p.GetFilesRecursivelyAsync("Y:\\source") == Array.Empty<SyncFile>().ToAsyncEnumerable() &&
                p.GetFilesRecursivelyAsync("Y:\\replica") == Array.Empty<SyncFile>().ToAsyncEnumerable() &&
                p.GetDirectoriesRecursively("Y:\\replica") ==
                new[] { new SyncDir("Y:\\replica", "01") }.ToAsyncEnumerable()
        );

        // act
        var syncService = new SyncService(dirService);
        var syncResult = syncService.SyncDirsAsync("Y:\\source", "Y:\\replica").Result;

        // assert 
        Assert.AreEqual(0, syncResult.MatchingDirs.Count());
        Assert.AreEqual(0, syncResult.CreatedDirs.Count());
        Assert.AreEqual(1, syncResult.DeletedDirs.Count());

        Assert.AreEqual(0, syncResult.MatchingFiles.Count());
        Assert.AreEqual(0, syncResult.CreatedFiles.Count());
        Assert.AreEqual(0, syncResult.DeletedFiles.Count());
        Assert.AreEqual(0, syncResult.MovedFiles.Count());

        Mock.Get(dirService).Verify(service => service.DeleteDir("Y:\\replica\\01"));
    }
}