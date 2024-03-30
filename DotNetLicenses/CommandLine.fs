// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.CommandLine

open System.IO

[<RequireQualifiedAccess>]
type Command =
    | PrintMetadata of inputDirectory: string
    | PrintHelp
    | PrintVersion

type ExitCode =
    | Success = 0
    | InvalidArguments = 1

let parse(args: string[]): struct(Command * ExitCode) =
    match args with
    | [| "--help" |] -> Command.PrintHelp, ExitCode.Success
    | [| "--version" |] -> Command.PrintVersion, ExitCode.Success
    | [| |] -> Command.PrintMetadata(Directory.GetCurrentDirectory()), ExitCode.Success
    | [| path |] -> Command.PrintMetadata path, ExitCode.Success
    | _ -> Command.PrintVersion, ExitCode.InvalidArguments
