// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.CommandLineParserTests

open DotNetLicenses.CommandLine
open Xunit

[<Fact>]
let ``print command is the default``(): unit =
    let path = "foo/bar.toml"
    Assert.Equal(struct(Command.PrintPackages path, ExitCode.Success), Parse [| path |])

[<Fact>]
let ``print command is supported``(): unit =
    let path = "foo/bar.toml"
    let command = Parse [| "print-packages"; path |]
    Assert.Equal(struct(Command.PrintPackages path, ExitCode.Success), command)

[<Fact>]
let ``generate-lock command is supported``(): unit =
    let path = "foo/bar.toml"
    let command = Parse [| "generate-lock"; path |]
    Assert.Equal(struct(Command.GenerateLock path, ExitCode.Success), command)

[<Fact>]
let ``Help option is supported``(): unit =
    Assert.Equal(struct(Command.PrintHelp, ExitCode.Success), Parse [| "--help" |])

[<Fact>]
let ``Version option is supported``(): unit =
    Assert.Equal(struct(Command.PrintVersion, ExitCode.Success), Parse [| "--version" |])
