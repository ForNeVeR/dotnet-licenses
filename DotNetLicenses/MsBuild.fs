// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.MsBuild

open System.Text.Json
open System.Threading.Tasks
open TruePath
open Medallion.Shell

[<CLIMutable>]
type MsBuildOutputRoot = {
    Items: MsBuildItems
}
and [<CLIMutable>] MsBuildItems = {
    PackageReference: MsBuildPackageReference[]
}
and [<CLIMutable>] MsBuildPackageReference =
    {
        Identity: string
        Version: string
    }

    member this.FromMsBuild() =
        {
            PackageId = this.Identity
            Version = this.Version
        }

let GetPackageReferences(projectFilePath: AbsolutePath): Task<PackageReference[]> = task {
    let! result = Command.Run("dotnet", "build", "-getItem:PackageReference", projectFilePath.Value).Task
    let stdOut = result.StandardOutput
    let stdErr = result.StandardError
    if not result.Success then
        let message =
            seq {
                $"Exit code: {result.ExitCode}"
                if stdOut.Length > 0 then $"Standard output: {stdOut}"
                if stdErr.Length > 0 then $"Standard error: {stdErr}"
            } |> String.concat "\n"
        failwith message

    let output = JsonSerializer.Deserialize<MsBuildOutputRoot> result.StandardOutput
    return output.Items.PackageReference |> Array.map _.FromMsBuild()
}

let GetProjectGeneratedArtifacts(projectFilePath: AbsolutePath): Task<AbsolutePath> = task {
    return failwith "TODO"
}
