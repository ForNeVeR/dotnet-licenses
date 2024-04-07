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
            baseDirectory |> Option.defaultWith(fun() -> Path.GetDirectoryName(Seq.exactlyOne config.Inputs))
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
            Inputs = [|"non-important.csproj"|]
            LockFile = null
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
                Inputs = [|"non-important.csproj"|]
                LockFile = lockFile
        }
        let! wp = runGenerator config
        Assert.Equal([|ExitCode.PackageIsNotDefined|], wp.Codes)
        Assert.Equal([|"package is not specified in the configuration."|], wp.Messages)
    finally
        File.Delete lockFile
}

[<Fact>]
let ``Printer generates warnings if there are stale overrides``(): Task =
    DataFiles.Deploy "Test.csproj" (fun project -> task {
        let config = {
            Configuration.Empty with
                Inputs = [|project|]
                Overrides = [|{
                    Id = "NonExistent"
                    Version = ""
                    Spdx = ""
                    Copyright = ""
                }|]
                LockFile = "lock.toml"
        }
        let! wp = runPrinter config

        Assert.Equivalent([|ExitCode.UnusedOverride|], wp.Codes)
        Assert.Equivalent([|
            """Unused overrides: id = "NonExistent", version = ""."""
        |], wp.Messages)
    })

[<Fact>]
let ``Printer generates no warnings on a valid config``(): Task =
    DataFiles.Deploy "Test.csproj" (fun project -> task {
        let config = {
            Configuration.Empty with
                Inputs = [|project|]
                LockFile = "lock.toml"
        }
        let! wp = runPrinter config

        Assert.Empty wp.Codes
        Assert.Empty wp.Messages
    })

[<Fact>]
let ``Processor generates a lock file``(): Task = DataFiles.Deploy "Test.csproj" (fun project -> task {
    let expectedLock = """[["*"]]
source_id = "FVNever.DotNetLicenses"
source_version = "1.0.0"
spdx = "MIT"
copyright = ""
""" // TODO[#25]: Should not be a star; reconsider after implementing the file sets
    let config = {
        Configuration.Empty with
            Inputs = [| project |]
            Overrides = [|
                {
                    Id = "FVNever.DotNetLicenses"
                    Version = "1.0.0"
                    Spdx = "MIT"
                    Copyright = ""
                }
            |]
            LockFile = Path.GetTempFileName()
    }

    let! wp = runGenerator config
    Assert.Empty wp.Codes
    Assert.Empty wp.Messages

    let! actualContent = File.ReadAllTextAsync config.LockFile
    Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
})


[<Fact>]
let ``Processor generates a lock file for a file tree``(): Task = task {
    use directory = DisposableDirectory.Create()
    let packagedFile = Path.Combine(directory.Path, "my-file.txt")
    do! File.WriteAllTextAsync(packagedFile, "Hello World!")
    let expectedLock = """[["my-file.txt"]]
id = "FVNever.DotNetLicenses"
version = "1.0.0"
spdx = "License FVNever.DotNetLicenses"
copyright = "Copyright FVNever.DotNetLicenses"
"""

    do! DataFiles.Deploy "Test.csproj" (fun project -> task {
        let lockFile = Path.Combine(directory.Path, "lock.toml")
        let config = {
            Configuration.Empty with
                Inputs = [| project |]
                LockFile = lockFile
                Package = [|
                    { Type = "file"; Path = directory.Path }
                |]
        }

        let! wp = runGenerator config
        Assert.Empty wp.Codes
        Assert.Empty wp.Messages

        let! actualContent = File.ReadAllTextAsync config.LockFile
        Assert.Equal(expectedLock.ReplaceLineEndings "\n", actualContent.ReplaceLineEndings "\n")
    })
}

[<Fact>]
let ``Processor generates a lock file for a ZIP archive``(): Task = task {
    Assert.Fail()
}

[<Fact>]
let ``Processor processes ZIP files using a glob pattern``(): Task = task {
    Assert.Fail()
}

[<Fact>]
let ``Lock file generator produces a warning if it's unable to find a license for a file``(): Task = task {
    use directory = DisposableDirectory.Create()
    let packagedFile = Path.Combine(directory.Path, "my-file.txt")
    do! File.WriteAllTextAsync(packagedFile, "Hello World!")
    let config = {
        Configuration.Empty with
            Inputs = [||]
            LockFile = Path.Combine(directory.Path, "lock.toml")
            Package = [|
                { Type = "file"; Path = directory.Path }
            |]
    }
    let! wp = Runner.RunFunction(Processor.GenerateLockFile, config, directory.Path)
    Assert.Equal([|ExitCode.LicenseForFileNotFound|], wp.Codes)
    let message = Assert.Single wp.Messages
    Assert.Equal("Unable to find a license for the file \"my-file.txt\".", message)
}
