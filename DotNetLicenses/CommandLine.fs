// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.CommandLine

open System
open TruePath

[<RequireQualifiedAccess>]
type Command =
    | GenerateLock of configFile: AbsolutePath
    | PrintPackages of configFile: AbsolutePath
    | Verify of configFile: AbsolutePath
    | DownloadLicenses of configFile: AbsolutePath
    | PrintHelp
    | PrintVersion

type ExitCode =
    // Should go from the less severe to the most severe, since in some conditions the max code is returned (e.g., if a
    // command produced several warnings).
    | Success = 0
    // Less important warnings:
    | UnusedOverride = 1
    | DuplicateOverride = 2
    | UnusedLockFileEntry = 3
    // More important warnings:
    | LicenseForFileNotFound = 10
    | LicenseSetEmpty = 11
    | NonEmptyIgnoredLockFileEntry = 12
    | FileNotCovered = 13
    // Configuration errors:
    | LockFileIsNotDefined = 20
    | PackagedFilesAreNotDefined = 21
    // Unable to start:
    | InvalidArguments = 255

let private resolvePath path =
    AbsolutePath(Environment.CurrentDirectory) / path

let Parse(args: string[]): struct(Command * ExitCode) =
    match args with
    | [| "--help" |] -> Command.PrintHelp, ExitCode.Success
    | [| "--version" |] -> Command.PrintVersion, ExitCode.Success
    | [| path |] -> Command.PrintPackages(resolvePath path), ExitCode.Success
    | [| "print-packages"; path |] -> Command.PrintPackages(resolvePath path), ExitCode.Success
    | [| "generate-lock"; path |] -> Command.GenerateLock(resolvePath path), ExitCode.Success
    | [| "verify"; path |] -> Command.Verify(resolvePath path), ExitCode.Success
    | [| "download-licenses"; path |] -> Command.DownloadLicenses(resolvePath path), ExitCode.Success
    | _ -> Command.PrintHelp, ExitCode.InvalidArguments
