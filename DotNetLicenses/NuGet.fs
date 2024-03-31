// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.NuGet

open System
open System.IO
open System.Threading.Tasks
open System.Xml
open System.Xml.Serialization
open DotNetLicenses.MsBuild

let internal PackagesFolderPath =
    Environment.GetEnvironmentVariable "NUGET_PACKAGES"
    |> Option.ofObj
    |> Option.defaultWith(fun() ->
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages")
    )

let GetNuSpecFilePath(packageReference: PackageReference): string =
    Path.Combine(
        PackagesFolderPath,
        packageReference.PackageId.ToLowerInvariant(),
        packageReference.Version.ToLowerInvariant(),
        $"{packageReference.PackageId.ToLowerInvariant()}.nuspec"
    )

[<CLIMutable>]
type NuSpecLicense = {
    [<XmlAttribute("type")>] Type: string
    [<XmlText>] Value: string
}

[<CLIMutable>]
type NuSpecMetadata = {
    [<XmlElement("id")>] Id: string
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
let ReadNuSpec(filePath: string): Task<NuSpec> = Task.Run(fun() ->
    use stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)
    use reader = new NoNamespaceXmlReader(stream)
    serializer.Deserialize(reader) :?> NuSpec
)
