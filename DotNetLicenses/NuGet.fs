// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.NuGet

open System
open System.IO
open System.Threading.Tasks
open System.Xml.Serialization
open DotNetLicenses.MSBuild

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

type NuSpecLicense = {
    [<XmlAttribute("type")>] Type: string
    [<XmlText>] Value: string
}

type NuSpecMetadata = {
    [<XmlElement("id")>] Id: string
    [<XmlElement("license")>] License: NuSpecLicense
    [<XmlElement("copyright")>] Copyright: string
}

type NuSpec = {
    [<XmlElement("metadata")>] Metadata: NuSpecMetadata
}

let private serializer = XmlSerializer typeof<NuSpec>
let ReadNuSpec(filePath: string): Task<NuSpec> = Task.Run(fun() ->
    use stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)
    serializer.Deserialize(stream) :?> NuSpec
)
