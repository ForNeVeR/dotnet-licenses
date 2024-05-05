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

let mutable internal PackagesFolderPath: AbsolutePath =
    Environment.GetEnvironmentVariable "NUGET_PACKAGES"
    |> Option.ofObj
    |> Option.map AbsolutePath
    |> Option.defaultWith(fun() ->
        AbsolutePath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) / ".nuget" / "packages"
    )

let internal UnpackedPackagePath(packageReference: PackageReference): AbsolutePath =
    match packageReference with
    | NuGetReference coordinates ->
        PackagesFolderPath / coordinates.PackageId.ToLowerInvariant() / coordinates.Version.ToLowerInvariant()
    | FrameworkReference coordinates -> failwith "TODO"

let internal GetNuSpecFilePath(packageReference: PackageCoordinates): AbsolutePath =
    UnpackedPackagePath(NuGetReference packageReference) / $"{packageReference.PackageId.ToLowerInvariant()}.nuspec"

let private GetPackageFiles package =
    Directory.EnumerateFileSystemEntries(
        (UnpackedPackagePath package).Value,
        "*",
        EnumerationOptions(RecurseSubdirectories = true, IgnoreInaccessible = false)
    )
    |> Seq.map AbsolutePath

let internal ContainsFile (fileHashCache: FileHashCache)
                          (package: PackageReference, entry: ISourceEntry): Task<bool> = task {
    let files = GetPackageFiles package
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

type NoNamespaceXmlReader(input: Stream) =
    inherit XmlTextReader(input)
    override this.NamespaceURI = ""

let private serializer = XmlSerializer typeof<NuSpec>
let internal ReadNuSpec(filePath: AbsolutePath): Task<NuSpec> = Task.Run(fun() ->
    use stream = File.Open(filePath.Value, FileMode.Open, FileAccess.Read, FileShare.Read)
    use reader = new NoNamespaceXmlReader(stream)
    serializer.Deserialize(reader) :?> NuSpec
)

type INuGetReader =
    abstract ReadNuSpec: PackageCoordinates -> Task<NuSpec>
    abstract ContainsFileName: PackageReference -> string -> Task<bool>
    abstract FindFile: FileHashCache -> PackageReference seq -> ISourceEntry -> Task<PackageReference[]>

type NuGetReader() =
    interface INuGetReader with
        member _.ReadNuSpec(coordinates: PackageCoordinates): Task<NuSpec> =
            GetNuSpecFilePath coordinates |> ReadNuSpec

        member _.ContainsFileName (package: PackageReference) (fileName: string): Task<bool> =
            task {
                do! Task.Yield()
                let files = GetPackageFiles package
                return files |> Seq.exists(fun f -> Path.GetFileName f.Value = fileName)
            }

        member _.FindFile (cache: FileHashCache)
                          (packages: PackageReference seq)
                          (entry: ISourceEntry): Task<PackageReference[]> = task {
            let! checkResults =
                packages
                |> Seq.map(fun p -> task {
                    let! checkResult = ContainsFile cache (p, entry)
                    return checkResult, p
                })
                |> Task.WhenAll
            return checkResults |> Seq.filter fst |> Seq.map snd |> Seq.toArray
        }

[<CLIMutable>]
type ProjectAssetsJson = {
    Version: int
    Libraries: Dictionary<string, LibraryAsset>
}
and [<CLIMutable>] LibraryAsset = {
    Type: string
}

let private SerializerOptions = JsonSerializerOptions(JsonSerializerDefaults.Web)

let private ReadProjectAssetsJson(file: AbsolutePath): Task<ProjectAssetsJson> = task {
    use stream = File.OpenRead file.Value
    return! JsonSerializer.DeserializeAsync<ProjectAssetsJson>(stream, SerializerOptions)
}

let private ReadProjectReferences(project: AbsolutePath): Task<PackageReference[]> = task {
    let! properties = MsBuild.GetProperties(project, MsBuild.DefaultConfiguration, [| "BaseIntermediateOutputPath" |])
    let objDir = LocalPath(properties["BaseIntermediateOutputPath"].Replace('\\', Path.DirectorySeparatorChar))
    let projectAssetsJson = project.Parent.Value / objDir / "project.assets.json"
    if File.Exists projectAssetsJson.Value then
        let! assets = ReadProjectAssetsJson projectAssetsJson
        let frameworkReferences = failwith "TODO"
        let nuGetReferences =
            assets.Libraries
            |> Seq.filter(fun e -> e.Value.Type = "package")
            |> Seq.map(fun e ->
                let entryName = e.Key
                match entryName.Split('/') with
                | [| packageId; version |] ->
                    NuGetReference { PackageId = packageId; Version = version }
                | _ -> failwithf $"Cannot read package reference in file \"{project}\": \"{entryName}\"."
            )
        return [|
            frameworkReferences
            nuGetReferences
        |] |> Seq.concat |> Seq.toArray
    else
        return! MsBuild.GetDirectPackageReferences project
}

let ReadTransitiveProjectReferences(projectOrSolution: AbsolutePath): Task<PackageReference[]> = task {
    let! projects = MsBuild.GetProjects projectOrSolution
    let! assetReferences =
        projects
        |> Seq.map ReadProjectReferences
        |> Task.WhenAll
    let allReferences =
        Seq.concat assetReferences
        |> Seq.distinct
        |> Seq.sortBy(function
            | FrameworkReference f -> 0, f.PackageId, f.Version
            | NuGetReference r -> 1, r.PackageId, r.Version
        )
        |> Seq.toArray
    return allReferences
}
