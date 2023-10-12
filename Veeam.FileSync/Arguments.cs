using CommandLine;

namespace Veeam.FileSync;

public class Arguments
{
    [Option('i', "syncInterval", Required = true, HelpText = "Set the synchronization interval in milliseconds.")]
    public double SyncInterval { get; set; }

    [Option('l', "logPath", Required = true, HelpText = "Set the path to the log file.")]
    public string LogPath { get; set; }

    [Option('s', "sourceDirPath", Required = true, HelpText = "Set the path source directory.")]
    public string SourceDirPath { get; set; }

    [Option('r', "replicaDirPath", Required = true, HelpText = "Set the path replica directory.")]
    public string ReplicaDirPath { get; set; }
}