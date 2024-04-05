// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT
module DotNetLicenses.Program

open DotNetLicenses.CommandLine

[<EntryPoint>]
let main(args: string[]): int =
    let struct(command, parseResult) = Parse args
    if parseResult = ExitCode.Success then
        Processor.Process Command.PrintHelp |> ignore
        int parseResult
    else
        Processor.Process command
