// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.CommandLine

[<RequireQualifiedAccess>]
type Command =
    | PrintMetadata of configFilePath: string
    | PrintHelp
    | PrintVersion

type ExitCode =
    // Should go from the less severe to the most severe, since in some conditions the max code is returned (e.g. if a
    // command produced several warnings).
    | Success = 0
    | UnusedOverride = 1
    | DuplicateOverride = 2
    | InvalidArguments = 255

let parse(args: string[]): struct(Command * ExitCode) =
    match args with
    | [| "--help" |] -> Command.PrintHelp, ExitCode.Success
    | [| "--version" |] -> Command.PrintVersion, ExitCode.Success
    | [| path |] -> Command.PrintMetadata path, ExitCode.Success
    | _ -> Command.PrintVersion, ExitCode.InvalidArguments
