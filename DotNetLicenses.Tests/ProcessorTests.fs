// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.ProcessorTests

open System.IO
open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.CommandLine
open DotNetLicenses.TestFramework
open Xunit

type private Runner =
    static member RunFunction(func: _ * _ * _ * _ -> Task, config, ?baseDirectory: string) = task {
        let baseDirectory =
            baseDirectory |> Option.defaultWith(fun() ->
                let (NuGetSource source) = Seq.exactlyOne config.MetadataSources
                Path.GetDirectoryName(source.Include)
            )
        let wp = WarningProcessor()
        do! func(
            config,
            baseDirectory,
            NuGetMock.MirroringReader,
            wp
        )
        return wp
    }

let private runPrinter t = Runner.RunFunction(Processor.PrintMetadata, t)
let private runGenerator t = Runner.RunFunction(Processor.GenerateLockFile, t)

[<Fact>]
let ``Generator produces an error if no lock file is defined``(): Task = task {
    let config = {
        Configuration.Empty with
            MetadataSources = [| Configuration.NuGet "non-important.csproj" |]
    }
    let! wp = runGenerator config
    Assert.Equal([|ExitCode.LockFileIsNotDefined|], wp.Codes)
    Assert.Equal([|"lock_file is not specified in the configuration."|], wp.Messages)
}

[<Fact>]
let ``Generator produces an error if no package contents are defined``(): Task = task {
    let lockFile = Path.GetTempFileName()
    try
        let config = {
            Configuration.Empty with
                MetadataSources = [| Configuration.NuGet "non-important.csproj" |]
                LockFilePath = Some lockFile
        }
        let! wp = runGenerator config
        Assert.Equal([|ExitCode.PackagedFilesAreNotDefined|], wp.Codes)
        Assert.Equal([|"packaged_files are not specified in the configuration."|], wp.Messages)
    finally
        File.Delete lockFile
}

[<Fact>]
let ``Printer generates warnings if there are stale overrides``(): Task =
    DataFiles.Deploy "Test.csproj" (fun project -> task {
        let config = {
            Configuration.Empty with
                MetadataSources = [| Configuration.NuGet project |]
                MetadataOverrides = [|{
                    Id = "NonExistent"
                    Version = ""
                    Spdx = ""
                    Copyright = ""
                }|]
                LockFilePath = Some "lock.toml"
        }
        let! wp = runPrinter config

        Assert.Equivalent([|ExitCode.UnusedOverride|], wp.Codes)
        Assert.Equivalent([|
            """Unused metadata overrides: id = "NonExistent", version = ""."""
        |], wp.Messages)
    })

[<Fact>]
let ``Printer generates no warnings on a valid config``(): Task =
    DataFiles.Deploy "Test.csproj" (fun project -> task {
        let config = {
            Configuration.Empty with
                MetadataSources = [| Configuration.NuGet project |]
                LockFilePath = Some "lock.toml"
        }
        let! wp = runPrinter config

        Assert.Empty wp.Codes
        Assert.Empty wp.Messages
    })

[<Fact>]
let ``Processor generates a lock file``(): Task = DataFiles.Deploy "Test.csproj" (fun project -> task {
    use directory = DisposableDirectory.Create()
    do! File.WriteAllTextAsync(Path.Combine(directory.Path, "test.txt"), "Hello!")

    let expectedLock = """[["test.txt"]]
source_id = "FVNever.DotNetLicenses"
source_version = "1.0.0"
spdx = "License FVNever.DotNetLicenses"
copyright = "Copyright FVNever.DotNetLicenses"
"""
    let config = {
        Configuration.Empty with
            MetadataSources = [| Configuration.NuGet project |]
            LockFilePath = Some <| Path.GetTempFileName()
            PackagedFiles = [|
                Directory directory.Path
            |]
    }

    let! wp = runGenerator config
    Assert.Empty wp.Codes
    Assert.Empty wp.Messages

    let! actualContent = File.ReadAllTextAsync <| Option.get config.LockFilePath
    Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
})

[<Fact>]
let ``Processor generates a lock file for a file tree``(): Task = task {
    use directory = DisposableDirectory.Create()
    let packagedFile = Path.Combine(directory.Path, "my-file.txt")
    do! File.WriteAllTextAsync(packagedFile, "Hello World!")
    let expectedLock = """[["my-file.txt"]]
source_id = "FVNever.DotNetLicenses"
source_version = "1.0.0"
spdx = "License FVNever.DotNetLicenses"
copyright = "Copyright FVNever.DotNetLicenses"
"""

    do! DataFiles.Deploy "Test.csproj" (fun project -> task {
        let lockFile = Path.Combine(directory.Path, "lock.toml")
        let config = {
            Configuration.Empty with
                MetadataSources = [| Configuration.NuGet project |]
                LockFilePath = Some lockFile
                PackagedFiles = [|
                    Directory directory.Path
                |]
        }

        let! wp = runGenerator config
        Assert.Empty wp.Codes
        Assert.Empty wp.Messages

        let! actualContent = File.ReadAllTextAsync <| Option.get config.LockFilePath
        Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
    })
}

[<Fact>]
let ``Processor generates a lock file for a ZIP archive``(): Task = task {
    use directory = DisposableDirectory.Create()
    let archivePath = Path.Combine(directory.Path, "file.zip")
    ZipFiles.SingleFileArchive(archivePath, "content/my-file.txt", "Hello World"B)
    let expectedLock = """[["content/my-file.txt"]]
source_id = "FVNever.DotNetLicenses"
source_version = "1.0.0"
spdx = "License FVNever.DotNetLicenses"
copyright = "Copyright FVNever.DotNetLicenses"
"""
    do! DataFiles.Deploy "Test.csproj" (fun project -> task {
        let lockFile = Path.Combine(directory.Path, "lock.toml")
        let config = {
            Configuration.Empty with
                MetadataSources = [| Configuration.NuGet project |]
                LockFilePath = Some lockFile
                PackagedFiles = [|
                    Zip <| Path.GetFileName archivePath
                |]
        }

        let! wp = Runner.RunFunction(Processor.GenerateLockFile, config, directory.Path)
        Assert.Empty wp.Codes
        Assert.Empty wp.Messages

        let! actualContent = File.ReadAllTextAsync <| Option.get config.LockFilePath
        Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
    })
}

[<Fact>]
let ``Processor processes ZIP files using a glob pattern``(): Task = task {
    use directory = DisposableDirectory.Create()
    let archivePath = Path.Combine(directory.Path, "file.zip")
    let archiveGlob = Path.Combine(directory.Path, "*.zip")
    ZipFiles.SingleFileArchive(archivePath, "content/my-file.txt", "Hello World"B)

    do! DataFiles.Deploy "Test.csproj" (fun project -> task {
        let lockFile = Path.Combine(directory.Path, "lock.toml")
        let config = {
            Configuration.Empty with
                MetadataSources = [| Configuration.NuGet project |]
                LockFilePath = Some lockFile
                PackagedFiles = [|
                    Zip archiveGlob
                |]
        }

        let! wp = runGenerator config
        Assert.Empty wp.Codes
        Assert.Empty wp.Messages
    })
}

[<Fact>]
let ``Lock file generator produces a warning if it's unable to find a license for a file``(): Task = task {
    use directory = DisposableDirectory.Create()
    let packagedFile = Path.Combine(directory.Path, "my-file.txt")
    do! File.WriteAllTextAsync(packagedFile, "Hello World!")
    let config = {
        Configuration.Empty with
            MetadataSources = [||]
            LockFilePath = Some <| Path.Combine(directory.Path, "lock.toml")
            PackagedFiles = [|
                Directory directory.Path
            |]
    }
    let! wp = Runner.RunFunction(Processor.GenerateLockFile, config, directory.Path)
    Assert.Equal([|ExitCode.LicenseForFileNotFound|], wp.Codes)
    let message = Assert.Single wp.Messages
    Assert.Equal("No license found for file \"my-file.txt\".", message)
}
