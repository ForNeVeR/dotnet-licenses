// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.MsBuild

open System.Collections.Generic
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

[<CLIMutable>]
type MultiPropertyMsBuildOutput = {
    Properties: Dictionary<string, string>
}

let private ExecuteDotNetBuild(args: string seq): Task<string> = task {
    let args = seq {
        box "build"
        yield! args |> Seq.cast<obj>
    }
    let! result = Command.Run("dotnet", arguments = args).Task
    if not result.Success then
        let message =
            seq {
                $"Exit code: {result.ExitCode}"
                if result.StandardOutput.Length > 0 then $"Standard output: {result.StandardOutput}"
                if result.StandardError.Length > 0 then $"Standard error: {result.StandardError}"
            } |> String.concat "\n"
        failwith message
    return result.StandardOutput
}

let private GetProperties(input: AbsolutePath,
                          configuration: string,
                          properties: string seq): Task<Dictionary<string, string>> = task {
    let! result = ExecuteDotNetBuild [|
        "--configuration"; configuration
        "-getProperty:" + String.concat "," properties
        input.Value
    |]
    let output = JsonSerializer.Deserialize<MultiPropertyMsBuildOutput> result
    return output.Properties
}

let GetPackageReferences(input: AbsolutePath): Task<PackageReference[]> = task {
    let! result = ExecuteDotNetBuild [| "-getItem:PackageReference"; input.Value |]
    let output = JsonSerializer.Deserialize<MsBuildOutputRoot> result
    return output.Items.PackageReference |> Array.map _.FromMsBuild()
}

let private Configuration = "Release"
let GetProjectGeneratedArtifacts(input: AbsolutePath): Task<AbsolutePath> = task {
    let! properties = GetProperties(input, Configuration, [| "TargetDir"; "TargetFileName" |])
    let targetDir = LocalPath properties["TargetDir"]
    let targetFileName = properties["TargetFileName"]
    let basePath = input.Parent.Value
    return basePath / targetDir / targetFileName
}
