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
    // Less important warnings:
    | UnusedOverride = 1
    | DuplicateOverride = 2
    // More important warnings:
    | LicenseForFileNotFound = 3
    // Configuration errors:
    | LockFileIsNotDefined = 5
    | PackagedFilesAreNotDefined = 6
    // Unable to start:
    | InvalidArguments = 255

let Parse(args: string[]): struct(Command * ExitCode) =
    match args with
    | [| "--help" |] -> Command.PrintHelp, ExitCode.Success
    | [| "--version" |] -> Command.PrintVersion, ExitCode.Success
    | [| path |] -> Command.PrintMetadata path, ExitCode.Success
    | [| "print"; path |] -> Command.PrintMetadata path, ExitCode.Success
    | [| "generate-lock"; path |] -> Command.GenerateLock path, ExitCode.Success
    | _ -> Command.PrintHelp, ExitCode.InvalidArguments
