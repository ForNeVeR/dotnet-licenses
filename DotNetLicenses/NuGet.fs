// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.NuGet

open System
open System.IO
open System.Threading.Tasks
open System.Xml
open System.Xml.Serialization
open DotNetLicenses.Sources

let mutable internal PackagesFolderPath =
    Environment.GetEnvironmentVariable "NUGET_PACKAGES"
    |> Option.ofObj
    |> Option.defaultWith(fun() ->
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages")
    )

let internal UnpackedPackagePath(packageReference: PackageReference): string =
    Path.Combine(
        PackagesFolderPath,
        packageReference.PackageId.ToLowerInvariant(),
        packageReference.Version.ToLowerInvariant()
    )

let internal GetNuSpecFilePath(packageReference: PackageReference): string =
    Path.Combine(
        UnpackedPackagePath packageReference,
        $"{packageReference.PackageId.ToLowerInvariant()}.nuspec"
    )

let internal ContainsFile (fileHashCache: FileHashCache)
                          (package: PackageReference, entry: SourceEntry): Task<bool> = task {
    let files = Directory.EnumerateFileSystemEntries(
        UnpackedPackagePath package,
        "*",
        EnumerationOptions(RecurseSubdirectories = true, IgnoreInaccessible = false)
    )
    let fileName = Path.GetFileName entry.FullPath
    let possibleFiles = files |> Seq.filter(fun f -> Path.GetFileName f = fileName)
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
let internal ReadNuSpec(filePath: string): Task<NuSpec> = Task.Run(fun() ->
    use stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)
    use reader = new NoNamespaceXmlReader(stream)
    serializer.Deserialize(reader) :?> NuSpec
)

type INuGetReader =
    abstract ReadNuSpec: PackageReference -> Task<NuSpec>
    abstract FindFile: FileHashCache -> PackageReference seq -> SourceEntry -> Task<PackageReference[]>

type NuGetReader() =
    interface INuGetReader with
        member _.ReadNuSpec(reference: PackageReference): Task<NuSpec> =
            GetNuSpecFilePath reference |> ReadNuSpec
        member _.FindFile (cache: FileHashCache)
                          (packages: PackageReference seq)
                          (entry: SourceEntry): Task<PackageReference[]> = task {
            let! checkResults =
                packages
                |> Seq.map(fun p -> task {
                    let! checkResult = ContainsFile cache (p, entry)
                    return checkResult, p
                })
                |> Task.WhenAll
            return checkResults |> Seq.filter fst |> Seq.map snd |> Seq.toArray
        }
