// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.MsBuild

open System.Text.Json
open System.Threading.Tasks
open Medallion.Shell

[<CLIMutable>]
type MsBuildOutput = {
    Items: MsBuildItems
}
and [<CLIMutable>] MsBuildItems = {
    PackageReference: MsBuildPackageReference[]
}
and [<CLIMutable>] MsBuildPackageReference = {
    Identity: string
    Version: string
}

type PackageReference =
    {
        PackageId: string
        Version: string
    }
    static member OfMsBuild(reference: MsBuildPackageReference) =
        {
            PackageId = reference.Identity
            Version = reference.Version
        }

let GetPackageReferences(projectFilePath: string): Task<PackageReference[]> = task {
    let! result = Command.Run("dotnet", "build", "-getItem:PackageReference", projectFilePath).Task
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

    let output = JsonSerializer.Deserialize<MsBuildOutput> result.StandardOutput
    return output.Items.PackageReference |> Seq.map PackageReference.OfMsBuild |> Seq.toArray
}
