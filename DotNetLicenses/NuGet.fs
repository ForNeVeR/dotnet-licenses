// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.NuGet

open System
open System.IO
open System.Threading.Tasks
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
        packageReference.PackageId,
        packageReference.Version,
        $"{packageReference.PackageId}.nuspec"
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
[<XmlRoot("package", Namespace = "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd")>]
type NuSpec = {
    [<XmlElement("metadata")>] Metadata: NuSpecMetadata
}

let private serializer = XmlSerializer typeof<NuSpec>
let ReadNuSpec(filePath: string): Task<NuSpec> = Task.Run(fun() ->
    use stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)
    serializer.Deserialize(stream) :?> NuSpec
)
