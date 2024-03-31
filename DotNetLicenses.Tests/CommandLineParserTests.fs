// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.CommandLineParserTests

open DotNetLicenses.CommandLine
open Xunit

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
