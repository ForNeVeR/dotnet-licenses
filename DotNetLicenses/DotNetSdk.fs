// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.DotNetSdk

open System
open System.IO
open System.Threading.Tasks
open TruePath

let Location: Task<AbsolutePath> = task {
    do! Task.Yield()
    let dotNetExecutable = if OperatingSystem.IsWindows() then "dotnet.exe" else "dotnet"
    let path = Environment.GetEnvironmentVariable "PATH"
    let paths = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
    let dotNetPath =
        paths
        |> Seq.find (fun p -> File.Exists(Path.Combine(p, dotNetExecutable)))
        |> AbsolutePath
    return dotNetPath
}

let SharedFrameworkLocation(framework: PackageCoordinates): Task<AbsolutePath> = task {
    let! sdkPath = Location
    return sdkPath / "shared" / framework.PackageId / framework.Version
}
