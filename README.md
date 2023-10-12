# Veeam.FileSync by Jan Vratislav
The **Veeam.FileSync** console application syncs two folder - a source and a replica.

 To start run the program with following params

     
    -i, --syncInterval      Required. Set the synchronization interval in milliseconds.
    -l, --logPath           Required. Set the path to the log file.
    -s, --sourceDirPath     Required. Set the path source directory.
    -r, --replicaDirPath    Required. Set the path replica directory.
    
    Example: Veeam.FileSync.exe -s c:\source -r c:\replica -i 2000 -l c:\logs\sync.log

