// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.ProcessorTests

open System.IO
open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.CommandLine
open DotNetLicenses.TestFramework
open TruePath
open Xunit

type private Runner =
    static member RunFunction(func: _ * _ * _ * _ -> Task, config, ?baseDirectory: AbsolutePath) = task {
        let baseDirectory =
            baseDirectory |> Option.defaultWith(fun() ->
                let path =
                    match Seq.exactlyOne config.MetadataSources with
                    | NuGet source -> source.Include
                    | other -> failwithf $"Unexpected metadata source: {other}"
                AbsolutePath.op_Implicit (StrictAbsolutePath path.Value).Parent.Value
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
            MetadataSources = [| NuGetSource.Of "non-important.csproj" |]
    }
    let! wp = runGenerator config
    Assert.Equal([|ExitCode.LockFileIsNotDefined|], wp.Codes)
    Assert.Equal([|"lock_file is not specified in the configuration."|], wp.Messages)
}

[<Fact>]
let ``Generator produces an error if no package contents are defined``(): Task = task {
    let lockFile = AbsolutePath <| Path.GetTempFileName()
    try
        let config = {
            Configuration.Empty with
                MetadataSources = [| NuGetSource.Of "non-important.csproj" |]
                LockFile = Some <| lockFile.AsRelative()
        }
        let! wp = runGenerator config
        Assert.Equal([|ExitCode.PackagedFilesAreNotDefined|], wp.Codes)
        Assert.Equal([|"packaged_files are not specified in the configuration."|], wp.Messages)
    finally
        File.Delete lockFile.Value
}

[<Fact>]
let ``Printer generates warnings if there are stale overrides``(): Task =
    DataFiles.Deploy "Test.csproj" (fun project -> task {
        let config = {
            Configuration.Empty with
                MetadataSources = [| NuGetSource.Of(project.AsRelative()) |]
                MetadataOverrides = [|{
                    Id = "NonExistent"
                    Version = ""
                    Spdx = ""
                    Copyright = ""
                }|]
                LockFile = Some <| RelativePath "lock.toml"
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
                MetadataSources = [| NuGetSource.Of(project.AsRelative()) |]
                LockFile = Some <| RelativePath "lock.toml"
        }
        let! wp = runPrinter config

        Assert.Empty wp.Codes
        Assert.Empty wp.Messages
    })

[<Fact>]
let ``Processor generates a lock file``(): Task = DataFiles.Deploy "Test.csproj" (fun project -> task {
    use directory = DisposableDirectory.Create()
    do! File.WriteAllTextAsync((directory.Path / "test.txt").Value, "Hello!")

    let expectedLock = """[["test.txt"]]
source_id = "FVNever.DotNetLicenses"
source_version = "1.0.0"
spdx = "License FVNever.DotNetLicenses"
copyright = "Copyright FVNever.DotNetLicenses"
"""
    let config = {
        Configuration.Empty with
            MetadataSources = [| NuGetSource.Of(project.AsRelative()) |]
            LockFile = Some(RelativePath <| Path.GetTempFileName())
            PackagedFiles = [|
                Directory <| directory.Path.AsRelative()
            |]
    }

    let! wp = runGenerator config
    Assert.Empty wp.Codes
    Assert.Empty wp.Messages

    let! actualContent = File.ReadAllTextAsync <| (Option.get config.LockFile).Value
    Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
})

[<Fact>]
let ``Processor generates a lock file for a file tree``(): Task = task {
    use directory = DisposableDirectory.Create()
    let packagedFile = directory.Path / "my-file.txt"
    do! File.WriteAllTextAsync(packagedFile.Value, "Hello World!")
    let expectedLock = """[["my-file.txt"]]
source_id = "FVNever.DotNetLicenses"
source_version = "1.0.0"
spdx = "License FVNever.DotNetLicenses"
copyright = "Copyright FVNever.DotNetLicenses"
"""

    do! DataFiles.Deploy "Test.csproj" (fun project -> task {
        let lockFile = directory.Path / "lock.toml"
        let config = {
            Configuration.Empty with
                MetadataSources = [| NuGetSource.Of(project.AsRelative()) |]
                LockFile = Some <| lockFile.AsRelative()
                PackagedFiles = [|
                    Directory <| directory.Path.AsRelative()
                |]
        }

        let! wp = runGenerator config
        Assert.Empty wp.Codes
        Assert.Empty wp.Messages

        let! actualContent = File.ReadAllTextAsync <| (Option.get config.LockFile).Value
        Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
    })
}

[<Fact>]
let ``Processor generates a lock file for a ZIP archive``(): Task = task {
    use directory = DisposableDirectory.Create()
    let archivePath = directory.Path / "file.zip"
    ZipFiles.SingleFileArchive(AbsolutePath.op_Implicit archivePath, "content/my-file.txt", "Hello World"B)
    let expectedLock = """[["content/my-file.txt"]]
source_id = "FVNever.DotNetLicenses"
source_version = "1.0.0"
spdx = "License FVNever.DotNetLicenses"
copyright = "Copyright FVNever.DotNetLicenses"
"""
    do! DataFiles.Deploy "Test.csproj" (fun project -> task {
        let lockFile = directory.Path / "lock.toml"
        let config = {
            Configuration.Empty with
                MetadataSources = [| NuGetSource.Of(project.AsRelative()) |]
                LockFile = Some <| lockFile.AsRelative()
                PackagedFiles = [|
                    Zip <| LocalPathPattern archivePath.FileName
                |]
        }

        let! wp = Runner.RunFunction(Processor.GenerateLockFile, config, directory.Path)
        Assert.Empty wp.Codes
        Assert.Empty wp.Messages

        let! actualContent = File.ReadAllTextAsync <| (Option.get config.LockFile).Value
        Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
    })
}

[<Fact>]
let ``Processor processes ZIP files using a glob pattern``(): Task = task {
    use directory = DisposableDirectory.Create()
    let archivePath = directory.Path / "file.zip"
    let archiveGlob = LocalPathPattern <| (directory.Path / "*.zip").Value
    ZipFiles.SingleFileArchive(AbsolutePath.op_Implicit archivePath, "content/my-file.txt", "Hello World"B)

    do! DataFiles.Deploy "Test.csproj" (fun project -> task {
        let lockFile = directory.Path / "lock.toml"
        let config = {
            Configuration.Empty with
                MetadataSources = [| NuGetSource.Of(project.AsRelative()) |]
                LockFile = Some <| lockFile.AsRelative()
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
    let packagedFile = directory.Path / "my-file.txt"
    do! File.WriteAllTextAsync(packagedFile.Value, "Hello World!")
    let config = {
        Configuration.Empty with
            MetadataSources = [||]
            LockFile = Some <| (directory.Path / "lock.toml").AsRelative()
            PackagedFiles = [|
                Directory <| directory.Path.AsRelative()
            |]
    }
    let! wp = Runner.RunFunction(Processor.GenerateLockFile, config, directory.Path)
    Assert.Equal([|ExitCode.LicenseForFileNotFound|], wp.Codes)
    let message = Assert.Single wp.Messages
    Assert.Equal("No license found for file \"my-file.txt\".", message)
}

[<Fact>]
let ``License metadata gets saved to the lock file``(): Task = task {
    use directory = DisposableDirectory.Create()
    let packagedFile = directory.Path / "my-file.txt"
    do! File.WriteAllTextAsync(packagedFile.Value, "")

    let expectedLock = """[["my-file.txt"]]
spdx = "MIT"
copyright = "Me"
"""

    let lockFile = directory.Path / "lock.toml"
    let config = {
        Configuration.Empty with
            MetadataSources = [|
                License { Spdx = "MIT"; Copyright = "Me"; FilesCovered = [| LocalPathPattern "*.txt" |] }
            |]
            LockFile = Some <| lockFile.AsRelative()
            PackagedFiles = [| Directory <| directory.Path.AsRelative() |]
    }

    let! wp = Runner.RunFunction(Processor.GenerateLockFile, config, baseDirectory = directory.Path)
    Assert.Empty wp.Codes
    Assert.Empty wp.Messages

    let! actualContent = File.ReadAllTextAsync <| (Option.get config.LockFile).Value
    Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
}
