// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.CommandLineParserTests

open System
open DotNetLicenses.CommandLine
open Xunit

[<Fact>]
let ``If the command is not passed then read the current directory``(): unit =
    Assert.Equal(struct(Command.PrintProjectMetadata Environment.CurrentDirectory, ExitCode.Success), parse Array.empty)

[<Fact>]
let ``Passed path is processed``(): unit =
    let path = "foo/bar"
    Assert.Equal(struct(Command.PrintProjectMetadata path, ExitCode.Success), parse [| path |])

[<Fact>]
let ``Help command is supported``(): unit =
    Assert.Equal(struct(Command.PrintHelp, ExitCode.Success), parse [| "--help" |])

[<Fact>]
let ``Version command is supported``(): unit =
    Assert.Equal(struct(Command.PrintVersion, ExitCode.Success), parse [| "--version" |])
