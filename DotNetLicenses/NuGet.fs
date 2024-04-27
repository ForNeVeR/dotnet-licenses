// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.NuGet

open System
open System.IO
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
    PackagesFolderPath / packageReference.PackageId.ToLowerInvariant() / packageReference.Version.ToLowerInvariant()

let internal GetNuSpecFilePath(packageReference: PackageReference): AbsolutePath =
    UnpackedPackagePath packageReference / $"{packageReference.PackageId.ToLowerInvariant()}.nuspec"

let internal ContainsFile (fileHashCache: FileHashCache)
                          (package: PackageReference, entry: ISourceEntry): Task<bool> = task {
    let files =
        Directory.EnumerateFileSystemEntries(
            (UnpackedPackagePath package).Value,
            "*",
            EnumerationOptions(RecurseSubdirectories = true, IgnoreInaccessible = false)
        )
        |> Seq.map AbsolutePath
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
    abstract ReadNuSpec: PackageReference -> Task<NuSpec>
    abstract FindFile: FileHashCache -> PackageReference seq -> ISourceEntry -> Task<PackageReference[]>

type NuGetReader() =
    interface INuGetReader with
        member _.ReadNuSpec(reference: PackageReference): Task<NuSpec> =
            GetNuSpecFilePath reference |> ReadNuSpec
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
