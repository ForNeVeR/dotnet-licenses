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

let private runFunction func config = task {
    let wp = WarningProcessor()
    let! exitCode = func(
        config,
        Path.GetDirectoryName(Seq.exactlyOne config.Inputs),
        NuGetMock.MirroringReader,
        wp
    )
    return wp, exitCode
}

let private runPrinter = runFunction Processor.PrintMetadata
let private runGenerator = runFunction Processor.GenerateLockFile

[<Fact>]
let ``Printer generates warnings if there are stale overrides``(): Task =
    DataFiles.Deploy "Test.csproj" (fun project -> task {
        let config = {
            Inputs = [|project|]
            Overrides = [|{
                Id = "NonExistent"
                Version = ""
                Spdx = ""
                Copyright = ""
            }|]
            LockFile = "lock.toml"
        }
        let! wp, exitCode = runPrinter config

        Assert.Equal(ExitCode.UnusedOverride, enum exitCode)
        Assert.Equivalent([|ExitCode.UnusedOverride|], wp.Codes)
        Assert.Equivalent([|
            """Unused overrides: id = "NonExistent", version = ""."""
        |], wp.Messages)
    })

[<Fact>]
let ``Printer generates no warnings on a valid config``(): Task =
    DataFiles.Deploy "Test.csproj" (fun project -> task {
        let config = {
            Inputs = [|project|]
            Overrides = null
            LockFile = "lock.toml"
        }
        let! wp, exitCode = runPrinter config

        Assert.Equal(ExitCode.Success, enum exitCode)
        Assert.Empty wp.Codes
        Assert.Empty wp.Messages
    })

[<Fact>]
let ``Processor generates a lock file``(): Task = DataFiles.Deploy "Test.csproj" (fun project -> task {
    let expectedLock = """
[default]
"*" = [{ id = "FVNever.DotNetLicenses"; version = "1.0.0"; spdx = "MIT"; copyright = "" }]
""" // TODO: Should not be a star; reconsider after implementing the file sets
    let config = {
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

    let! wp, exitCode = runGenerator config
    Assert.Equal(ExitCode.Success, enum exitCode)
    Assert.Empty wp.Codes
    Assert.Empty wp.Messages

    let! actualContent = File.ReadAllTextAsync config.LockFile
    Assert.Equal(expectedLock, actualContent)
})
