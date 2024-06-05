// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.MsBuild

open System.Collections.Generic
open System.IO
open System.Text.Json
open System.Threading.Tasks
open Microsoft.Build.Construction
open TruePath
open Medallion.Shell

[<CLIMutable>]
type MsBuildOutputRoot = {
    Items: MsBuildItems
}
and [<CLIMutable>] MsBuildItem = {
    Identity: string
}
and [<CLIMutable>] MsBuildItems = {
    DebugSymbolsProjectOutputGroupOutput: MsBuildItem[]
    KnownFrameworkReference: KnownFrameworkReference[]
    PackageReference: MsBuildPackageReference[]
    ResolvedFrameworkReference: ResolvedFrameworkReference[]
    ResolvedTargetingPack: ResolvedTargetingPack[]
    SourceRoot: MsBuildItem[]
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

and [<CLIMutable>] KnownFrameworkReference = {
    TargetFramework: string
    Identity: string
    LatestRuntimeFrameworkVersion: string
}

and [<CLIMutable>] ResolvedFrameworkReference = {
    Identity: string
    NuGetPackageVersion: string
}
and [<CLIMutable>] ResolvedTargetingPack = {
    Identity: string
    NuGetPackageVersion: string
    RuntimeFrameworkName: string
    RuntimePackRuntimeIdentifiers: string
}

[<CLIMutable>]
type MultiPropertyMsBuildOutput = {
    Properties: Dictionary<string, string>
}

let internal ExecuteDotNet(args: string seq): Task<string> = task {
    let args = args |> Seq.cast<obj>
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

let private ExecuteDotNetBuild(args: string seq): Task<string> =
    ExecuteDotNet (seq {
        "build"
        yield! args
    })

let internal GetProperties(input: AbsolutePath,
                          configuration: string,
                          runtime: string option,
                          properties: string[]): Task<Dictionary<string, string>> = task {
    let! result = ExecuteDotNetBuild [|
        "--configuration"; configuration
        "-getProperty:" + String.concat "," properties
        match runtime with
        | Some r -> "--runtime"; r
        | None -> ()
        input.Value
    |]

    if properties.Length = 1 then
        let dict = Dictionary()
        dict[Array.exactlyOne properties] <- result.TrimEnd [| '\r'; '\n' |]
        return dict
    else
        let output = JsonSerializer.Deserialize<MultiPropertyMsBuildOutput> result
        return output.Properties
}

let private GetItems (input: AbsolutePath)
                     (configuration: string option)
                     (taskName: string option)
                     (itemGroups: string seq)
                     (transformer: MsBuildItems -> 'a): Task<'a> = task {
    let! result = ExecuteDotNetBuild [|
        match configuration with
        | Some c -> "--configuration"; c
        | None -> ()

        match taskName with
        | Some t -> "-target:" + t
        | None -> ()

        "-getItem:" + String.concat "," itemGroups
        input.Value
    |]
    let output = JsonSerializer.Deserialize<MsBuildOutputRoot> result
    return transformer output.Items
}

let GetProjects(input: AbsolutePath): Task<AbsolutePath[]> = task {
    match Path.GetExtension input.Value with
    | ".sln" ->
        do! Task.Yield()
        let solution = SolutionFile.Parse(input.Value)
        let projectPaths =
            solution.ProjectsInOrder
            |> Seq.filter (fun p -> p.ProjectType <> SolutionProjectType.SolutionFolder)
            |> Seq.map (fun p -> AbsolutePath p.AbsolutePath)
            |> Seq.toArray
        return projectPaths
    | _ -> return [| input |] // assume it's a project file
}

let internal DefaultConfiguration = "Release"

let internal GetDirectPackageReferences(input: AbsolutePath): Task<PackageReference[]> = task {
    let! projects = GetProjects input
    let! references =
        projects
        |> Seq.map(fun project -> task {
            let! references = GetItems project None None [| "PackageReference" |] _.PackageReference
            return references |> Seq.map _.FromMsBuild() |> Seq.map(fun ref -> project, ref) |> Seq.toArray
        })
        |> Task.WhenAll
    return references
           |> Seq.collect id
           |> Seq.distinct
           |> Seq.sortBy(fun (p, x) -> p.Value, x.PackageId, x.Version)
           |> Seq.map NuGetReference
           |> Seq.toArray
}

type ProjectGeneratedArtifacts =
    {
        FilesWithContent: AbsolutePath[]
        FilePatterns: LocalPathPattern[]
    }
    static member Merge(items: ProjectGeneratedArtifacts[]): ProjectGeneratedArtifacts = {
        FilesWithContent = items |> Seq.collect _.FilesWithContent |> Seq.distinct |> Seq.sortBy _.Value |> Array.ofSeq
        FilePatterns = items |> Seq.collect _.FilePatterns |> Seq.distinct |> Seq.sortBy _.Value |> Array.ofSeq
    }

let internal GetKnownFrameworkReferences(project: AbsolutePath): Task<KnownFrameworkReference[]> = task {
    return! GetItems project (Some DefaultConfiguration) None [| "KnownFrameworkReference" |] _.KnownFrameworkReference
}

let private GetProjectGeneratedArtifacts (runtime: string option)
                                         (input: AbsolutePath): Task<ProjectGeneratedArtifacts> = task {
    let! properties = GetProperties(input, DefaultConfiguration, runtime, [|
        "BaseIntermediateOutputPath"
        "TargetName"
        "TargetDir"; "TargetFileName"
        "UseAppHost"
    |])
    let properties =
        properties
        |> Seq.map(fun kv -> kv.Key, kv.Value)
        |> Map.ofSeq
        // Replace MSBuild's path separator with the platform's path separator
        |> Map.map(fun _ v -> v.Replace('\\', Path.DirectorySeparatorChar))

    let targetDir = LocalPath properties["TargetDir"]
    let targetFileName = properties["TargetFileName"]
    let basePath = input.Parent.Value
    let target = basePath / targetDir / targetFileName
    let objDir = basePath / properties["BaseIntermediateOutputPath"]
    let targetName = properties["TargetName"]
    let useAppHost = properties["UseAppHost"] = "true"
    let artifacts = [|
        target
        basePath / targetDir / $"{targetName}.runtimeconfig.json"
        // dotnet tools:
        objDir / "DotNetToolSettings.xml"
    |]
    let isDllProject = Path.GetExtension targetFileName = ".dll"
    let patterns = [|
        LocalPathPattern $"**/{targetName}.pdb"
        LocalPathPattern $"**/{targetName}.deps.json"
        if useAppHost && isDllProject then
            LocalPathPattern $"**/{targetName}.exe"
    |]
    return {
        FilesWithContent = artifacts
        FilePatterns = patterns
    }
}

let internal GetRuntimePackReferences(project: AbsolutePath): Task<PackageReference[]> = task {
    let! frameworkReferences, targetingPacks =
        GetItems
            project
            (Some DefaultConfiguration)
            (Some "ResolveFrameworkReferences")
            [| "ResolvedFrameworkReference"; "ResolvedTargetingPack" |]
            (fun out -> out.ResolvedFrameworkReference, out.ResolvedTargetingPack)
    let targetingPacks =
        targetingPacks
        |> Seq.map(fun pack -> pack.Identity, pack)
        |> Map.ofSeq
    let runtimePackReferences =
        frameworkReferences
        |> Seq.map(fun framework -> targetingPacks[framework.Identity])
        |> Seq.collect(fun targetingPack ->
            let baseRuntimePackName = $"{targetingPack.RuntimeFrameworkName}.runtime"
            targetingPack.RuntimePackRuntimeIdentifiers.Split(';')
            |> Seq.map(fun id -> $"{baseRuntimePackName}.{id}")
            |> Seq.map(fun package -> NuGetReference(project, {
                PackageId = package
                Version = targetingPack.NuGetPackageVersion
            }))
        )
        |> Seq.toArray
    return runtimePackReferences
}

let GetGeneratedArtifacts(input: AbsolutePath, runtime: string option): Task<ProjectGeneratedArtifacts> = task {
    let! projects = GetProjects input
    let! artifacts =
        projects
        |> Seq.map (GetProjectGeneratedArtifacts runtime)
        |> Task.WhenAll
    return artifacts |> ProjectGeneratedArtifacts.Merge
}

let private LoadSourceRoots(input: AbsolutePath): Task<MsBuildItem[]> = task {
    let! projects = GetProjects input
    let! items =
        projects
        |> Seq.map(fun p -> GetItems p None (Some "Restore") [| "SourceRoot" |] _.SourceRoot)
        |> Task.WhenAll
    return items |> Array.collect id
}


let GetSourceRoots (cache: MsBuildItemCache<MsBuildItem>) (input: AbsolutePath): Task<AbsolutePath[]> = task {
    let! items = cache.Get input LoadSourceRoots
    return items |> Array.map (fun item -> AbsolutePath item.Identity)
}
