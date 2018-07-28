mathats
=======

matroska file parser written in C# for .NET Core


Usage
-----

```
mathats [commands] [options] [file]
```

* ### Commands:
```
parse        parse matroska file (read-only mode).
suid <hex>   write 128-bit hexadecimal to file's suid.
suid void    void file's suid (overwrite suid).
```

* ### Options:
```
noCrc        don't validate crc checksum.
keepDate     keep file's date when writing.
help         show help.
```


Get started
-----------

1. Install [.NET Core SDK 2.1.300](https://github.com/dotnet/core/blob/master/release-notes/download-archives/2.1.0-download.md) or [greater](https://github.com/dotnet/core/tree/master/release-notes)

2. Choose [type](https://docs.microsoft.com/dotnet/core/deploying) of deployment:
   - Self-contained deployment (SCD):  
     - Windows 10 64-bit:  
       Run `dotnet publish -c release -r win10-x64`
     - [RID](https://docs.microsoft.com/dotnet/core/rid-catalog) listed platform:  
       Run `dotnet publish -c release -r <RID>`
   - Framework-dependent deployment (FDD):  
     Run `dotnet publish -c release`

3. Change into directory of deployment and get help:
   - SCD: Run `mathats help`
   - FDD: Run `dotnet mathats.dll help`


Credits
-------

- Joe (windows testing)
- Hattie (proofreading)
- you? (mac/linux testing)


License
-------

MIT
