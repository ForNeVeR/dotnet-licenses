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

let private runProcessor config = task {
    let wp = WarningProcessor()
    let! exitCode = Processor.ProcessPrintMetadata(
        config,
        Path.GetDirectoryName(Seq.exactlyOne config.Inputs),
        NuGetMock.MirroringReader,
        wp
    )
    return wp, exitCode
}


[<Fact>]
let ``Processor generates warnings if there are stale overrides``(): Task =
    DataFiles.Deploy "Test.csproj" (fun project -> task {
        let config = {
            Inputs = [|project|]
            Overrides = [|{
                Id = "NonExistent"
                Version = ""
                Spdx = ""
                Copyright = ""
            }|]
        }
        let! wp, exitCode = runProcessor config

        Assert.Equal(ExitCode.UnusedOverride, enum exitCode)
        Assert.Equivalent([|ExitCode.UnusedOverride|], wp.Codes)
        Assert.Equivalent([|
            """Unused overrides: id = "NonExistent", version = ""."""
        |], wp.Messages)
    })

[<Fact>]
let ``Processor generates no warnings on a valid config``(): Task =
    DataFiles.Deploy "Test.csproj" (fun project -> task {
        let config = {
            Inputs = [|project|]
            Overrides = null
        }
        let! wp, exitCode = runProcessor config

        Assert.Equal(ExitCode.Success, enum exitCode)
        Assert.Empty wp.Codes
        Assert.Empty wp.Messages
    })
