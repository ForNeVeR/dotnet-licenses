// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.NuGet

open System
open System.Collections.Generic
open System.IO
open System.Text.Json
open System.Threading.Tasks
open System.Xml
open System.Xml.Serialization
open TruePath

[<CLIMutable>]
type NuSpecLicense = {
    [<XmlAttribute("type")>] Type: string
    [<XmlText>] Value: string
}

[<CLIMutable>]
type NuSpecMetadata = {
    [<XmlElement("id")>] Id: string
    [<XmlElement("version")>] Version: string
    [<XmlElement("license")>] License: NuSpecLicense
    [<XmlElement("copyright")>] Copyright: string
}

[<CLIMutable>]
// NOTE: the actual .nuspec files often have some package, such as
// - http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd
// - http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd
// In this type, we won't set any package, and will hack it using a NoNamespaceXmlReader.
[<XmlRoot("package")>]
type NuSpec = {
    [<XmlElement("metadata")>] Metadata: NuSpecMetadata
}

type INuGetReader =
    abstract ReadNuSpec: input: AbsolutePath -> PackageCoordinates -> Task<NuSpec option>
    abstract ReadPackageReferences: AbsolutePath -> Task<PackageReference[]>
    abstract ContainsFileName: AbsolutePath -> PackageReference -> string -> Task<bool>
    abstract FindFile: FileHashCache -> PackageReference seq -> ISourceEntry -> Task<PackageReference[]>

let internal UnpackedPackagePath (packagesRoot: AbsolutePath) (packageReference: PackageReference): AbsolutePath =
    match packageReference with
    | NuGetReference(_, coordinates) ->
        packagesRoot / coordinates.PackageId.ToLowerInvariant() / coordinates.Version.ToLowerInvariant()
    | FrameworkReference _ -> failwith "Not supported."

let private ExistingUnpackedPackagePath sourceRoots coords = task {
    do! Task.Yield()

    let packagePaths =
        sourceRoots
        |> Seq.map(fun root -> UnpackedPackagePath root (NuGetReference(AbsolutePath "/", coords))) // TODO: Consider removing the AbsolutePath from here after dropping the FrameworkReference: perhaps UnpackedPackagePath may accept PackageCoordinates instead?
        |> Seq.toArray

    return packagePaths |> Seq.filter(fun p -> Directory.Exists p.Value) |> Seq.tryHead
}


[<RequireQualifiedAccess>]
type RootResolveBehavior = Existing | Any

let internal GetNuSpecFilePath (sourceRoots: AbsolutePath seq)
                               (package: PackageCoordinates)
                               (resolveBehavior: RootResolveBehavior): Task<AbsolutePath option> = task {
    let! directory =
        match resolveBehavior with
        | RootResolveBehavior.Any ->
            let root = Seq.head sourceRoots
            Some(UnpackedPackagePath root (NuGetReference(root, package)))
            |> Task.FromResult
        | RootResolveBehavior.Existing -> ExistingUnpackedPackagePath sourceRoots package
    return directory |> Option.map (fun d -> d / $"{package.PackageId.ToLowerInvariant()}.nuspec")
}

let private GetPackageFiles sourceRoots (package: PackageCoordinates) = task {
    let! folder = ExistingUnpackedPackagePath sourceRoots package
    return (
        folder
        |> Option.toArray
        |> Seq.collect(fun folder ->
            Directory.EnumerateFileSystemEntries(
                folder.Value,
                "*",
                EnumerationOptions(RecurseSubdirectories = true, IgnoreInaccessible = false)
            )
        )
        |> Seq.map AbsolutePath
    )
}

let internal ContainsFile (fileHashCache: FileHashCache)
                          (sourceRoots: AbsolutePath seq)
                          (package: PackageReference, entry: ISourceEntry): Task<bool> = task {
    let coords = match package with NuGetReference(_, c) -> c | FrameworkReference c -> c
    let! files = GetPackageFiles sourceRoots coords
    let fileName = Path.GetFileName entry.SourceRelativePath
    let possibleFiles = files |> Seq.filter(fun f -> f.FileName = fileName)
    let! possibleFileCheckResults =
        possibleFiles
        |> Seq.map(fun f -> task {
            let! needleHash = entry.CalculateHash()
            let! packageFileHash = fileHashCache.CalculateFileHash f
            return needleHash.Equals(packageFileHash, StringComparison.OrdinalIgnoreCase)
        })
        |> Task.WhenAll
    return Seq.exists id possibleFileCheckResults
}

type NoNamespaceXmlReader(input: Stream) =
    inherit XmlTextReader(input)
    override this.NamespaceURI = ""

let private serializer = XmlSerializer typeof<NuSpec>
let internal ReadNuSpec(filePath: AbsolutePath): Task<NuSpec> = Task.Run(fun() ->
    use stream = File.Open(filePath.Value, FileMode.Open, FileAccess.Read, FileShare.Read)
    use reader = new NoNamespaceXmlReader(stream)
    serializer.Deserialize(reader) :?> NuSpec
)

[<CLIMutable>]
type ProjectAssetsJson = {
    Version: int
    Libraries: Dictionary<string, LibraryAsset>
    Project: ProjectAssetsProject
}
and [<CLIMutable>] LibraryAsset = {
    Type: string
}
and [<CLIMutable>] ProjectAssetsProject = {
    Frameworks: Dictionary<string, ProjectAssetsFramework>
}
and [<CLIMutable>] ProjectAssetsFramework = {
    FrameworkReferences: Dictionary<string, Dictionary<string, string>>
}

[<CLIMutable>]
type DepsJson = {
    Targets: Dictionary<string, Dictionary<string, obj>>
}

let private SerializerOptions = JsonSerializerOptions(JsonSerializerDefaults.Web)

let private ReadProjectAssetsJson(file: AbsolutePath): Task<ProjectAssetsJson> = task {
    use stream = File.OpenRead file.Value
    return! JsonSerializer.DeserializeAsync<ProjectAssetsJson>(stream, SerializerOptions)
}

let private ReadDepsJson(file: AbsolutePath): Task<DepsJson> = task {
    use stream = File.OpenRead file.Value
    return! JsonSerializer.DeserializeAsync<DepsJson>(stream, SerializerOptions)
}

let private ReadNuGetReferencesForFramework project coordinates = task {
    let! directory = DotNetSdk.SharedFrameworkLocation coordinates
    let depsJsonFile = directory / $"{coordinates.PackageId}.deps.json"
    if File.Exists depsJsonFile.Value then
        let! depsJson = ReadDepsJson depsJsonFile
        let packages = depsJson.Targets.Values |> Seq.collect _.Keys
        return packages |> Seq.map(fun packageSpec ->
            let nameAndVersion = packageSpec.Split('/', 2)
            match nameAndVersion with
            | [| name; version |] -> NuGetReference(project, { PackageId = name; Version = version })
            | _ -> failwithf $"Invalid name/version spec in \"{depsJsonFile}\": \"{packageSpec}\"."
        )

    else
        return [||]
}

[<Flags>]
type ReferenceType =
    /// <summary><c>&lt;PackageReference&gt;</c> elements from project.</summary>
    | PackageReference = 0b1
    /// Assets from <c>project.assets.json</c> — transitive.
    | ProjectAssets = 0b10
    /// Runtime pack references, used for publish.
    | RuntimePackReferences = 0b100
    | All = 0b111

let private ReadPackageReferencesFromProject (referenceType: ReferenceType)
                                             (project: AbsolutePath): Task<ResizeArray<PackageReference>> = task {
    let result = ResizeArray()

    if referenceType.HasFlag ReferenceType.PackageReference then
        let! packageReferences = MsBuild.GetDirectPackageReferences project
        result.AddRange packageReferences

    let properties = lazy MsBuild.GetProperties(
        project,
        MsBuild.DefaultConfiguration,
        None,
        [| "BaseIntermediateOutputPath" |]
    )
    let objDir = lazy task {
        let! properties = properties.Value
        return LocalPath(properties["BaseIntermediateOutputPath"].Replace('\\', Path.DirectorySeparatorChar))
    }

    if referenceType.HasFlag ReferenceType.ProjectAssets then
        let! objDir = objDir.Value
        let projectAssetsJson = project.Parent.Value / objDir / "project.assets.json"
        if File.Exists projectAssetsJson.Value then
            let! assets = ReadProjectAssetsJson projectAssetsJson
            let nuGetReferences =
                assets.Libraries
                |> Seq.filter(fun e -> e.Value.Type = "package")
                |> Seq.map(fun e ->
                    let entryName = e.Key
                    match entryName.Split('/') with
                    | [| packageId; version |] ->
                        NuGetReference(project, { PackageId = packageId; Version = version })
                    | _ -> failwithf $"Cannot read package reference in file \"{project}\": \"{entryName}\"."
                )
            result.AddRange nuGetReferences

    if referenceType.HasFlag ReferenceType.RuntimePackReferences then
        let! runtimePackReferences = MsBuild.GetRuntimePackReferences project
        result.AddRange runtimePackReferences

    return result
}

let ReadPackageReferences(
    projectOrSolution: AbsolutePath,
    referenceType: ReferenceType
): Task<PackageReference[]> = task {
    let! projects = MsBuild.GetProjects projectOrSolution
    let! projectReferences =
        projects
        |> Seq.map(ReadPackageReferencesFromProject referenceType)
        |> Task.WhenAll
    let allReferences =
        Seq.concat projectReferences
        |> Seq.distinct
        |> Seq.sortBy(function
            | FrameworkReference f -> 0, f.PackageId, f.Version
            | NuGetReference(_, r) -> 1, r.PackageId, r.Version
        )
    return allReferences |> Seq.distinct |> Seq.toArray
}

type NuGetReader() =
    let itemCache = MsBuildItemCache()

    interface INuGetReader with
        member _.ReadNuSpec (input: AbsolutePath) (coordinates: PackageCoordinates): Task<NuSpec option> = task {
            let! sourceRoots = MsBuild.GetSourceRoots itemCache input
            let! nuSpec = GetNuSpecFilePath sourceRoots coordinates RootResolveBehavior.Existing
            match nuSpec with
            | Some n ->
                let! result = ReadNuSpec n
                return Some result
            | None -> return None
        }

        member _.ReadPackageReferences input = ReadPackageReferences(input, ReferenceType.All)

        member _.ContainsFileName (input: AbsolutePath) (package: PackageReference) (fileName: string): Task<bool> =
            task {

                let! sourceRoots = MsBuild.GetSourceRoots itemCache input
                let coords = match package with NuGetReference(_, c) -> c | FrameworkReference c -> c
                let! files = GetPackageFiles sourceRoots coords
                return files |> Seq.exists(fun f -> Path.GetFileName f.Value = fileName)
            }

        member _.FindFile (cache: FileHashCache)
                          (packages: PackageReference seq)
                          (entry: ISourceEntry): Task<PackageReference[]> = task {
            let! checkResults =
                packages
                |> Seq.map(fun p -> task {
                    // TODO: Source root resolve cache, shared between invocations.
                    let! sourceRoots =
                        match p with
                        | NuGetReference(project, _) -> MsBuild.GetSourceRoots itemCache project
                        | _ -> failwithf $"Reference not supported: {p}."
                    let! checkResult = ContainsFile cache sourceRoots (p, entry)
                    return checkResult, p
                })
                |> Task.WhenAll
            return checkResults |> Seq.filter fst |> Seq.map snd |> Seq.toArray
        }
