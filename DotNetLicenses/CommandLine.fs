// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.CommandLine

[<RequireQualifiedAccess>]
type Command =
    | GenerateLock of configFilePath: string
    | PrintMetadata of configFilePath: string
    | PrintHelp
    | PrintVersion

type ExitCode =
    // Should go from the less severe to the most severe, since in some conditions the max code is returned (e.g., if a
    // command produced several warnings).
    | Success = 0
    | UnusedOverride = 1
    | DuplicateOverride = 2
    | LockFileDoesNotExist = 3
    | LockFileIsNotDefined = 4
    | PackageIsNotDefined = 5
    | InvalidArguments = 255

let Parse(args: string[]): struct(Command * ExitCode) =
    match args with
    | [| "--help" |] -> Command.PrintHelp, ExitCode.Success
    | [| "--version" |] -> Command.PrintVersion, ExitCode.Success
    | [| path |] -> Command.PrintMetadata path, ExitCode.Success
    | [| "print"; path |] -> Command.PrintMetadata path, ExitCode.Success
    | [| "generate-lock"; path |] -> Command.GenerateLock path, ExitCode.Success
    | _ -> Command.PrintHelp, ExitCode.InvalidArguments
