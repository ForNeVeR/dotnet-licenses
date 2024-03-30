// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT
module DotNetLicenses.Program

open DotNetLicenses.CommandLine

[<EntryPoint>]
let main(args: string[]): int =
    let struct(command, exitCode) = parse args
    let command = if exitCode = ExitCode.Success then command else Command.PrintHelp
    Processor.Perform command
    int exitCode
