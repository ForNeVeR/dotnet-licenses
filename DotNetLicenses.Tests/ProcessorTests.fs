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
                (AbsolutePath path.Value).Parent.Value
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

let private runPrinter t = Runner.RunFunction(Processor.PrintPackages, t)
let private runGenerator t = Runner.RunFunction(Processor.GenerateLockFile, t)

[<Fact>]
let ``Generator produces an error if no lock file is defined``(): Task = task {
    use directory = DisposableDirectory.Create()
    let config = {
        Configuration.Empty with
            MetadataSources = [| NuGetSource.Of directory.Path |]
    }
    let! wp = runGenerator config
    Assert.Equal([|ExitCode.LockFileIsNotDefined|], wp.Codes)
    Assert.Equal([|"lock_file is not specified in the configuration."|], wp.Messages)
}

[<Fact>]
let ``Generator produces an error if no package contents are defined``(): Task = task {
    use directory = DisposableDirectory.Create()
    let lockFile = directory.Path / "lock-file.toml"
    try
        let config = {
            Configuration.Empty with
                MetadataSources = [| NuGetSource.Of directory.Path |]
                LockFile = Some <| LocalPath.op_Implicit lockFile
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
                MetadataSources = [| NuGetSource.Of project |]
                MetadataOverrides = [|{
                    Id = "NonExistent"
                    Version = ""
                    Spdx = ""
                    Copyright = ""
                }|]
                LockFile = Some <| LocalPath "lock.toml"
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
                MetadataSources = [| NuGetSource.Of project |]
                LockFile = Some <| LocalPath "lock.toml"
        }
        let! wp = runPrinter config

        Assert.Empty wp.Codes
        Assert.Empty wp.Messages
    })

[<Fact>]
let ``Processor generates a lock file``(): Task = DataFiles.Deploy "Test.csproj" (fun project -> task {
    use directory = DisposableDirectory.Create()
    do! File.WriteAllTextAsync((directory.Path / "test.txt").Value, "Hello!")

    let expectedLock = """"test.txt" = [{source_id = "FVNever.DotNetLicenses", source_version = "1.0.0", spdx = ["License FVNever.DotNetLicenses"], copyright = ["Copyright FVNever.DotNetLicenses"]}]
"""
    let config = {
        Configuration.Empty with
            MetadataSources = [| NuGetSource.Of project |]
            LockFile = Some <| LocalPath(Path.GetTempFileName())
            PackagedFiles = [|
                PackageSpecs.Directory directory.Path
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
    let expectedLock = """"my-file.txt" = [{source_id = "FVNever.DotNetLicenses", source_version = "1.0.0", spdx = ["License FVNever.DotNetLicenses"], copyright = ["Copyright FVNever.DotNetLicenses"]}]
"""

    do! DataFiles.Deploy "Test.csproj" (fun project -> task {
        let lockFile = directory.Path / "lock.toml"
        let config = {
            Configuration.Empty with
                MetadataSources = [| NuGetSource.Of project |]
                LockFile = Some <| LocalPath.op_Implicit lockFile
                PackagedFiles = [|
                    PackageSpecs.Directory directory.Path
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
    ZipFiles.SingleFileArchive(archivePath, "content/my-file.txt", "Hello World"B)
    let expectedLock = """"content/my-file.txt" = [{source_id = "FVNever.DotNetLicenses", source_version = "1.0.0", spdx = ["License FVNever.DotNetLicenses"], copyright = ["Copyright FVNever.DotNetLicenses"]}]
"""
    do! DataFiles.Deploy "Test.csproj" (fun project -> task {
        let lockFile = directory.Path / "lock.toml"
        let config = {
            Configuration.Empty with
                MetadataSources = [| NuGetSource.Of project |]
                LockFile = Some <| LocalPath.op_Implicit lockFile
                PackagedFiles = [|
                    PackageSpecs.Zip archivePath.FileName
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
    let archiveGlob = (directory.Path / "*.zip").Value
    ZipFiles.SingleFileArchive(archivePath, "content/my-file.txt", "Hello World"B)

    do! DataFiles.Deploy "Test.csproj" (fun project -> task {
        let lockFile = directory.Path / "lock.toml"
        let config = {
            Configuration.Empty with
                MetadataSources = [| NuGetSource.Of project |]
                LockFile = Some <| LocalPath.op_Implicit lockFile
                PackagedFiles = [|
                    PackageSpecs.Zip archiveGlob
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
            LockFile = Some <| LocalPath.op_Implicit(directory.Path / "lock.toml")
            PackagedFiles = [|
                PackageSpecs.Directory directory.Path
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

    let expectedLock = """"my-file.txt" = [{spdx = ["MIT"], copyright = ["Me"]}]
"""

    let lockFile = directory.Path / "lock.toml"
    let config = {
        Configuration.Empty with
            MetadataSources = [|
                License {
                    Spdx = "MIT"
                    Copyright = "Me"
                    FilesCovered = [| LocalPathPattern "*.txt" |]
                    PatternsCovered = Array.empty
                }
            |]
            LockFile = Some <| LocalPath.op_Implicit lockFile
            PackagedFiles = [| PackageSpecs.Directory directory.Path |]
    }

    let! wp = Runner.RunFunction(Processor.GenerateLockFile, config, baseDirectory = directory.Path)
    Assert.Empty wp.Codes
    Assert.Empty wp.Messages

    let! actualContent = File.ReadAllTextAsync <| (Option.get config.LockFile).Value
    Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
}

[<Fact>]
let ``Basic REUSE specification works``(): Task = task {
    use directory = DisposableDirectory.Create()
    directory.MakeSubDirs [| LocalPath "source"; LocalPath "package" |]

    let sourceFile = directory.Path / "source" / "my-file.txt"
    let packagedFile = directory.Path / "package" / "my-file.txt"

    let fileContent = """SPDX-FileCopyrightText: 2024 Me
SPDX-License-Identifier: MIT
text
"""
    do! File.WriteAllTextAsync(packagedFile.Value, fileContent)
    do! File.WriteAllTextAsync(sourceFile.Value, fileContent)

    let expectedLock = """"my-file.txt" = [{spdx = ["MIT"], copyright = ["2024 Me"]}]
"""

    let lockFile = directory.Path / "lock.toml"
    let config = {
        Configuration.Empty with
            MetadataSources = [|
                ReuseSource.Of(directory.Path / "source")
            |]
            LockFile = Some <| LocalPath.op_Implicit lockFile
            PackagedFiles = [| PackageSpecs.Directory(directory.Path / "package") |]
    }

    let! wp = Runner.RunFunction(Processor.GenerateLockFile, config, baseDirectory = directory.Path)
    Assert.Empty wp.Codes
    Assert.Empty wp.Messages

    let! actualContent = File.ReadAllTextAsync <| (Option.get config.LockFile).Value
    Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
}

[<Fact>]
let ``DEP5 part of the REUSE specification works``(): Task = task {
    use directory = DisposableDirectory.Create()
    directory.MakeSubDirs [| LocalPath "package"; LocalPath "source/.reuse" |]

    let sourceFile = directory.Path / "source" / "my-file.txt"
    let packagedFile = directory.Path / "package" / "my-file.txt"

    let fileContent = "Hello World!"
    do! File.WriteAllTextAsync(packagedFile.Value, fileContent)
    do! File.WriteAllTextAsync(sourceFile.Value, fileContent)

    let dep5 = directory.Path / "source" / ".reuse" / "dep5"
    do! File.WriteAllTextAsync(dep5.Value, """Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
Upstream-Name: dotnet-licenses
Upstream-Contact: Friedrich von Never <friedrich@fornever.me>
Source: https://github.com/ForNeVeR/dotnet-licenses

Files: *.txt
Copyright: 2024 Me
License: MIT
    """)

    let expectedLock = """"my-file.txt" = [{spdx = ["MIT"], copyright = ["2024 Me"]}]
"""

    let lockFile = directory.Path / "lock.toml"
    let config = {
        Configuration.Empty with
            MetadataSources = [|
                ReuseSource.Of(LocalPath.op_Implicit(directory.Path / "source"))
            |]
            LockFile = Some <| LocalPath.op_Implicit lockFile
            PackagedFiles = [| PackageSpecs.Directory(directory.Path / "package") |]
    }

    let! wp = Runner.RunFunction(Processor.GenerateLockFile, config, baseDirectory = directory.Path)
    Assert.Empty wp.Messages
    Assert.Empty wp.Codes

    let! actualContent = File.ReadAllTextAsync <| (Option.get config.LockFile).Value
    Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
}

[<Fact>]
let ``Combined licenses from REUSE source work``(): Task = task {
    use directory = DisposableDirectory.Create()
    directory.MakeSubDirs [| LocalPath "source"; LocalPath "package" |]

    let mitFile = directory.Path / "source" / "my-mit-file.txt"
    let cc0File = directory.Path / "source" / "my-cc0-file.txt"
    let ccBy1File = directory.Path / "source" / "my-cc-by-1-file.txt"
    let ccBy3File = directory.Path / "source" / "my-cc-by-3-file.txt"
    let ccBy4File = directory.Path / "source" / "my-cc-by-4-file.txt"
    let myMitFile = directory.Path / "package" / "my-mit-file.txt"
    let myCombinedFile = directory.Path / "package" / "my-combined-file.txt"

    let mitFileContent = """SPDX-FileCopyrightText: 2024 Me
SPDX-License-Identifier: MIT
text
"""
    do! File.WriteAllTextAsync(mitFile.Value, mitFileContent)
    do! File.WriteAllTextAsync(myMitFile.Value, mitFileContent)
    do! File.WriteAllTextAsync(myCombinedFile.Value, "My Combined Content")

    do! File.WriteAllTextAsync(cc0File.Value, """SPDX-FileCopyrightText: 2024 Me
SPDX-License-Identifier: CC-0
text
    """)
    do! File.WriteAllTextAsync(ccBy3File.Value, """SPDX-FileCopyrightText: 2024 Me
SPDX-License-Identifier: CC-BY-3.0
text
    """)
    do! File.WriteAllTextAsync(ccBy1File.Value, "text")
    do! File.WriteAllTextAsync(
        (directory.Path / "source" / "my-cc-by-1-file.txt.license").Value,
        """SPDX-FileCopyrightText: 2024 Me

SPDX-License-Identifier: CC-BY-1.0
    """)
    do! File.WriteAllTextAsync(ccBy4File.Value, """SPDX-FileCopyrightText: 2024 Me
SPDX-License-Identifier: CC-BY-4.0
text
    """)

    do! File.WriteAllTextAsync((directory.Path / "source" / ".gitignore").Value, "/my-cc-by-3-file.txt")

    let expectedLock = """"my-combined-file.txt" = [{spdx = ["CC-BY-1.0", "CC-BY-4.0", "CC-0", "MIT"], copyright = ["2024 Me"]}]
"my-mit-file.txt" = [{spdx = ["MIT"], copyright = ["2024 Me"]}]
"""

    let lockFile = directory.Path / "lock.toml"
    let config = {
        Configuration.Empty with
            MetadataSources = [|
                Reuse {
                    Root = LocalPath.op_Implicit(directory.Path / "source")
                    Exclude = [| LocalPath.op_Implicit ccBy3File |]
                    FilesCovered = [| LocalPathPattern myCombinedFile.FileName |]
                    PatternsCovered = Array.empty
                }
            |]
            LockFile = Some <| LocalPath.op_Implicit lockFile
            PackagedFiles = [| PackageSpecs.Directory(directory.Path / "package") |]
    }

    let! wp = Runner.RunFunction(Processor.GenerateLockFile, config, baseDirectory = directory.Path)
    Assert.Empty wp.Messages
    Assert.Empty wp.Codes

    let! actualContent = File.ReadAllTextAsync <| (Option.get config.LockFile).Value
    Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
}

[<Fact>]
let ``Package cover spec works``(): Task =
    DataFiles.Deploy "Test.csproj" (fun project -> task {
        let baseDir = project.Parent.Value
        let! projectOutput = MsBuild.GetGeneratedArtifacts project
        projectOutput.FilesWithContent
        |> Array.iter (fun p -> Directory.CreateDirectory p.Parent.Value.Value |> ignore)

        let targetAssembly = projectOutput.FilesWithContent |> Array.head
        let outDir = targetAssembly.Parent.Value
        let config = {
            Configuration.Empty with
                MetadataSources = [|
                    License {
                        Spdx = "MIT"
                        Copyright = "Package cover spec works"
                        FilesCovered = Array.empty
                        PatternsCovered = [|
                            MsBuildCoverage(LocalPath project)
                        |]
                    }
                |]
                LockFile = Some <| LocalPath "lock.toml"
                PackagedFiles = [|
                    PackageSpecs.Directory outDir
                |]
        }

        do! File.WriteAllTextAsync(targetAssembly.Value, "Test file")
        let expectedLock = """"Test.dll" = [{spdx = ["MIT"], copyright = ["Package cover spec works"]}]
"""

        let! wp = Runner.RunFunction(Processor.GenerateLockFile, config, baseDirectory = baseDir)
        Assert.Empty wp.Messages
        Assert.Empty wp.Codes

        let! actualContent = File.ReadAllTextAsync <| (baseDir / (Option.get config.LockFile)).Value
        Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
    })
