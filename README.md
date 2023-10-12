# Veeam.FileSync by Jan Vratislav
The **Veeam.FileSync** console application syncs two folder - a source and a replica.

 To start run the program with following params

     
    -i, --syncInterval      Required. Set the synchronization interval in milliseconds.
    -l, --logPath           Required. Set the path to the log file.
    -s, --sourceDirPath     Required. Set the path source directory.
    -r, --replicaDirPath    Required. Set the path replica directory.
    
    Example: 
    Veeam.FileSync.exe -s c:\temp\source -r c:\temp\replica -i 5000 -l c:\logs\sync.log

    Ouput:
    Starting a sync operation from 'c:\Temp\source' > 'c:\Temp\replica'
    Press any key to quit

    D+ Veeam.FileSync
    D+ Veeam.FileSync.Tests
    D+ Veeam.FileSync\Properties
    D+ Veeam.FileSync\Services
    D+ Veeam.FileSync\Properties\PublishProfiles
    D+ Veeam.FileSync\Services\Impl
    F+ 'Veeam.FileSync.sln'
    F+ 'Veeam.FileSync\Arguments.cs'
    F+ 'Veeam.FileSync\log4net.config'
    F+ 'Veeam.FileSync\Program.cs'
    F+ 'README.md'
    F+ '.gitattributes'
    F+ '.gitignore'
    F+ 'Veeam.FileSync.Tests\SyncServiceTests.cs'
    F+ 'Veeam.FileSync\Services\ISyncService.cs'
    F+ 'Veeam.FileSync\Services\IDirService.cs'
    F+ 'Veeam.FileSync\Veeam.FileSync.csproj'
    F+ 'Veeam.FileSync.Tests\Veeam.FileSync.Tests.csproj'
    F+ 'Veeam.FileSync\Properties\launchSettings.json'
    F+ 'Veeam.FileSync\Veeam.FileSync.csproj.user'
    F+ 'Veeam.FileSync\Services\IHashService.cs'
    F+ 'Veeam.FileSync\Properties\PublishProfiles\FolderProfile.pubxml'
    F+ 'Veeam.FileSync\Services\SyncResult.cs'
    F+ 'Veeam.FileSync\Services\SyncFile.cs'
    F+ 'Veeam.FileSync\Properties\PublishProfiles\FolderProfile.pubxml.user'
    F+ 'Veeam.FileSync\Services\Impl\DirService.cs'
    F+ 'Veeam.FileSync\Services\SyncDir.cs'
    F+ 'Veeam.FileSync\Services\Impl\SyncService.cs'
    F+ 'Veeam.FileSync\Services\Impl\HashService.cs'
    Completed the sync operation in 00:00:00.1213784:
    D=:0    D+:22   D-:0
    F=:0    F+:36   F-:0    F>:0
    Legend: F is file, D is directory, = is matching, + is created, - is deleted, > is moved
    
    Starting a sync operation from 'c:\Temp\source' > 'c:\Temp\replica'
    Completed the sync operation in 00:00:00.0737533:
    D=:22   D+:0    D-:0
    F=:36   F+:0    F-:0    F>:0
    Legend: F is file, D is directory, = is matching, + is created, - is deleted, > is moved
    
    Starting a sync operation from 'c:\Temp\source' > 'c:\Temp\replica'
    F- 'Veeam.FileSync.Tests\Veeam.FileSync.Tests.csproj'
    F- 'Veeam.FileSync.Tests\SyncServiceTests.cs'
    D- 'Veeam.FileSync.Tests'
    Completed the sync operation in 00:00:00.0257321:
    D=:13   D+:0    D-:9
    F=:28   F+:0    F-:8    F>:0
    Legend: F is file, D is directory, = is matching, + is created, - is deleted, > is moved

    Waiting to complete current sync operation...
    
