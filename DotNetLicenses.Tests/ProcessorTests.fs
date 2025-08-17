// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.ProcessorTests

open System.IO
open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.CommandLine
open DotNetLicenses.NuGet
open DotNetLicenses.TestFramework
open Medallion.Shell
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

let private runPrinter t = Runner.RunFunction(Processor.PrintMetadata, t)
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
                    SpdxExpression = ""
                    CopyrightNotices = [|""|]
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

    let expectedLock = """# REUSE-IgnoreStart
"test.txt" = [{source_id = "FVNever.DotNetLicenses", spdx = "License FVNever.DotNetLicenses", copyright = ["Copyright FVNever.DotNetLicenses"]}]
# REUSE-IgnoreEnd
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
    let expectedLock = """# REUSE-IgnoreStart
"my-file.txt" = [{source_id = "FVNever.DotNetLicenses", spdx = "License FVNever.DotNetLicenses", copyright = ["Copyright FVNever.DotNetLicenses"]}]
# REUSE-IgnoreEnd
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
    let expectedLock = """# REUSE-IgnoreStart
"content/my-file.txt" = [{source_id = "FVNever.DotNetLicenses", spdx = "License FVNever.DotNetLicenses", copyright = ["Copyright FVNever.DotNetLicenses"]}]
# REUSE-IgnoreEnd
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

    let expectedLock = """# REUSE-IgnoreStart
"my-file.txt" = [{spdx = "MIT", copyright = ["Me"]}]
# REUSE-IgnoreEnd
"""

    let lockFile = directory.Path / "lock.toml"
    let config = {
        Configuration.Empty with
            MetadataSources = [|
                License {
                    SpdxExpression = "MIT"
                    CopyrightNotice = "Me"
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

    // REUSE-IgnoreStart
    let fileContent = """SPDX-FileCopyrightText: 2024 Me
SPDX-License-Identifier: MIT
text
"""
    // REUSE-IgnoreEnd
    do! File.WriteAllTextAsync(packagedFile.Value, fileContent)
    do! File.WriteAllTextAsync(sourceFile.Value, fileContent)

    let expectedLock = """# REUSE-IgnoreStart
"my-file.txt" = [{spdx = "MIT", copyright = ["2024 Me"]}]
# REUSE-IgnoreEnd
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

    let expectedLock = """# REUSE-IgnoreStart
"my-file.txt" = [{spdx = "MIT", copyright = ["2024 Me"]}]
# REUSE-IgnoreEnd
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

    // REUSE-IgnoreStart
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

    // REUSE-IgnoreEnd

    let expectedLock = """# REUSE-IgnoreStart
"my-combined-file.txt" = [{spdx = "CC-BY-1.0 AND CC-BY-4.0 AND CC-0 AND MIT", copyright = ["2024 Me"]}]
"my-mit-file.txt" = [{spdx = "MIT", copyright = ["2024 Me"]}]
# REUSE-IgnoreEnd
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
        let! projectOutput = MsBuild.GetGeneratedArtifacts(project, None)
        let files, _ = GeneratedArtifacts.Split projectOutput
        files
        |> Seq.iter (fun p -> Directory.CreateDirectory p.Parent.Value.Value |> ignore)

        let targetAssembly = files |> Seq.head
        let outDir = targetAssembly.Parent.Value
        let config = {
            Configuration.Empty with
                MetadataSources = [|
                    License {
                        SpdxExpression = "MIT"
                        CopyrightNotice = "Package cover spec works"
                        FilesCovered = Array.empty
                        PatternsCovered = [|
                            MsBuildCoverage(LocalPath project, None)
                        |]
                    }
                |]
                LockFile = Some <| LocalPath "lock.toml"
                PackagedFiles = [|
                    PackageSpecs.Directory outDir
                |]
        }

        do! File.WriteAllTextAsync(targetAssembly.Value, "Test file")
        let expectedLock = """# REUSE-IgnoreStart
"Test.dll" = [{spdx = "MIT", copyright = ["Package cover spec works"]}]
# REUSE-IgnoreEnd
"""

        let! wp = Runner.RunFunction(Processor.GenerateLockFile, config, baseDirectory = baseDir)
        Assert.Empty wp.Messages
        Assert.Empty wp.Codes

        let! actualContent = File.ReadAllTextAsync <| (baseDir / (Option.get config.LockFile)).Value
        Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
    })

[<Fact>]
let ``Verify works correctly on a happy path``(): Task = task {
    use dir = DisposableDirectory.Create()
    let subDir = dir.Path / "subdir"
    Directory.CreateDirectory subDir.Value |> ignore

    let file = subDir / "file.txt"
    do! File.WriteAllTextAsync(file.Value, "Hello, world!")
    let lockFileContent = """# REUSE-IgnoreStart
"file.txt" = [{source_id = "FVNever.DotNetLicenses", source_version = "1.0.0", spdx = "License FVNever.DotNetLicenses", copyright = ["Copyright FVNever.DotNetLicenses"]}]
# REUSE-IgnoreEnd
"""
    let lockFile = dir.Path / "lock.toml"
    do! File.WriteAllTextAsync(lockFile.Value, lockFileContent)

    let config = {
        Configuration.Empty with
            LockFile = Some <| LocalPath lockFile
            PackagedFiles = [|
                { Source = Directory <| LocalPath subDir; Ignores = Array.empty }
            |]
    }

    let wp = WarningProcessor()
    do! Processor.Verify(config, dir.Path, wp)
    Assert.Empty wp.Messages
    Assert.Empty wp.Codes
}

[<Fact>]
let ``Verify works correctly on an unhappy path``(): Task = task {
    use dir = DisposableDirectory.Create()
    let subDir = dir.Path / "subdir"
    Directory.CreateDirectory subDir.Value |> ignore

    let file = subDir / "file1.txt"
    do! File.WriteAllTextAsync(file.Value, "Hello, world!")
    let lockFileContent = """# REUSE-IgnoreStart
"file.txt" = [{source_id = "FVNever.DotNetLicenses", source_version = "1.0.0", spdx = "License FVNever.DotNetLicenses", copyright = ["Copyright FVNever.DotNetLicenses"]}]
# REUSE-IgnoreEnd
"""
    let lockFile = dir.Path / "lock.toml"
    do! File.WriteAllTextAsync(lockFile.Value, lockFileContent)

    let config = {
        Configuration.Empty with
            LockFile = Some <| LocalPath lockFile
            PackagedFiles = [|
                { Source = Directory <| LocalPath subDir; Ignores = Array.empty }
            |]
    }

    let wp = WarningProcessor()
    do! Processor.Verify(config, dir.Path, wp)
    Assert.Equivalent([|ExitCode.UnusedLockFileEntry; ExitCode.FileNotCovered|], wp.Codes)
    Assert.Equal<_>(
        [|"File \"file1.txt\" is not covered by the lock file."; "Lock file entry \"file.txt\" is not used."|],
        wp.Messages
    )
}

let private PublishProject (project: AbsolutePath) (publishDir: AbsolutePath) = task {
    let! executionResult =
        Command.Run(
            "dotnet",
            "publish",
            "--output", publishDir.Value,
            "--self-contained",
            project.Value
        ).Task
    if not executionResult.Success then
        let message = String.concat "\n" [|
            $"Exit code of dotnet publish: {executionResult.ExitCode}"
            if executionResult.StandardOutput.Length > 0 then
                $"Standard output: {executionResult.StandardOutput}"
            if executionResult.StandardError.Length > 0 then
                $"Standard error: {executionResult.StandardError}"
        |]
        Assert.Fail message
}

[<Fact>]
let ``Metadata for a self-contained application``(): Task = task {
    use dir = DisposableDirectory.Create()
    let sourceDir = dir.Path / "src"
    dir.MakeSubDirs [| LocalPath sourceDir |]

    let projectInTextFolder = DataFiles.Get "TestExe.csproj"
    let project = sourceDir / "TestExe.csproj"
    File.Copy(projectInTextFolder.Value, project.Value)
    File.Copy((DataFiles.Get "Program.cs").Value, (sourceDir / "Program.cs").Value)

    let publishDir = dir.Path / "publish"
    do! PublishProject project publishDir

    let lockFile = dir.Path / "lock.toml"

    let config = {
        Configuration.Empty with
            MetadataSources = [|
                NuGet {
                    Include = LocalPath project
                }
                License {
                    SpdxExpression = "MIT"
                    CopyrightNotice = "me"
                    FilesCovered = Array.empty
                    PatternsCovered = [|
                        MsBuildCoverage(LocalPath project, Some "win-x64") // TODO: Portable test on macOS/Linux
                    |]
                }
            |]
            LockFile = Some(LocalPath lockFile)
            PackagedFiles = [|
                {
                    Source = Directory(LocalPath publishDir)
                    Ignores = Array.empty
                }
            |]
        }

    let wp = WarningProcessor()
    do! Processor.GenerateLockFile(config, dir.Path, NuGetReader(), wp)
    Assert.Empty wp.Messages
    Assert.Empty wp.Codes

    let! lockFileItems = LockFile.ReadLockFile lockFile
    let exeFileLicense = lockFileItems[LocalPathPattern "TestExe.exe"] |> Assert.Single
    Assert.Equal(Some "MIT AND LicenseRef-DotNetSdk", exeFileLicense.SpdxExpression)
}

[<Fact>]
let ``Multiple similar licenses for same artifact don't give a warning``(): Task =
    DataFiles.Deploy "TestComplex.csproj" (fun project -> task {
        let! packages = MsBuild.GetDirectPackageReferences project
        Assert.Equal(2, packages.Length)

        use deployDir = DisposableDirectory.Create()
        let packagedFile = deployDir.Path / "my-file.txt"
        do! File.WriteAllTextAsync(packagedFile.Value, "Hello World!")

        let lockFile = deployDir.Path / "lock.toml"
        let config = {
            Configuration.Empty with
                MetadataSources = [|
                    NuGetSource.Of project
                |]
                LockFile = Some <| LocalPath lockFile
                PackagedFiles = [|
                    PackageSpecs.Directory deployDir.Path
                |]
        }

        let nuGet = NuGetMock.MockedNuGetReader(licenseOverride = Some "MIT")
        let wp = WarningProcessor()
        do! Processor.GenerateLockFile(
            config,
            project.Parent.Value,
            nuGet,
            wp
        )

        Assert.Empty wp.Codes
        Assert.Empty wp.Messages

        let expectedLock = """# REUSE-IgnoreStart
"my-file.txt" = [{source_id = "FVNever.Package1", source_version = "0.0.0", spdx = "MIT", copyright = ["Copyright FVNever.Package1"]}, {source_id = "FVNever.Package3", source_version = "0.0.0", spdx = "MIT", copyright = ["Copyright FVNever.Package3"]}]
# REUSE-IgnoreEnd
"""
        let! actualLock = File.ReadAllTextAsync lockFile.Value
        Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualLock.ReplaceLineEndings "\n")
    })

[<Fact>]
let ``Multiple different licenses for same artifact generate a warning``(): Task =
    DataFiles.Deploy "TestComplex.csproj" (fun project -> task {
        let! packages = MsBuild.GetDirectPackageReferences project
        Assert.Equal(2, packages.Length)

        use deployDir = DisposableDirectory.Create()
        let packagedFile = deployDir.Path / "my-file.txt"
        do! File.WriteAllTextAsync(packagedFile.Value, "Hello World!")

        let lockFile = deployDir.Path / "lock.toml"
        let config = {
            Configuration.Empty with
                MetadataSources = [|
                    NuGetSource.Of project
                |]
                LockFile = Some <| LocalPath lockFile
                PackagedFiles = [|
                    PackageSpecs.Directory deployDir.Path
                |]
        }

        let! result = runGenerator config
        Assert.Equal([|ExitCode.DifferentLicenseSetsForFile|], result.Codes)
        Assert.Equal(
            "Multiple different license sets for file \"my-file.txt\":" +
            " [License FVNever.Package1] (FVNever.Package1 0.0.0)," +
            " [License FVNever.Package3] (FVNever.Package3 0.0.0)."
        , Assert.Single result.Messages)

        let expectedLock = """# REUSE-IgnoreStart
"my-file.txt" = [{source_id = "FVNever.Package1", source_version = "0.0.0", spdx = "License FVNever.Package1", copyright = ["Copyright FVNever.Package1"]}, {source_id = "FVNever.Package3", source_version = "0.0.0", spdx = "License FVNever.Package3", copyright = ["Copyright FVNever.Package3"]}]
# REUSE-IgnoreEnd
"""
        let! actualLock = File.ReadAllTextAsync lockFile.Value
        Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualLock.ReplaceLineEndings "\n")
    })
