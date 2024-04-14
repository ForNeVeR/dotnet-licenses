// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Metadata

open System.Collections.Generic
open System.Collections.ObjectModel
open System.Threading.Tasks
open DotNetLicenses.MsBuild
open DotNetLicenses.NuGet
open TruePath

type MetadataItem =
    | Package of {| Source: PackageReference; Spdx: string; Copyright: string |}
    | License of LicenseSource

let internal GetMetadata (source: PackageReference) (nuSpec: NuSpec): MetadataItem =
    let metadata = nuSpec.Metadata
    let license = metadata.License
    if license.Type <> "expression" then
        failwithf $"Unsupported license type: {license.Type}"
    Package {|
        Source = source
        Spdx = metadata.License.Value
        Copyright = metadata.Copyright
    |}

type MetadataReadResult = {
    Items: ReadOnlyCollection<MetadataItem>
    UsedOverrides: Set<PackageReference>
}

type MetadataReader(nuGet: INuGetReader) =
    member _.ReadFromProject(
        projectFilePath: AbsolutePath,
        overrides: IReadOnlyDictionary<PackageReference, MetadataOverride>
    ): Task<MetadataReadResult> = task {
        let mutable usedOverrides = Set.empty

        let! packageReferences = GetPackageReferences projectFilePath
        let result = ResizeArray(packageReferences.Length)
        for reference in packageReferences do
            let! metadata =
                match overrides.TryGetValue reference with
                | true, metaOverride ->
                    usedOverrides <- usedOverrides |> Set.add reference
                    Task.FromResult <| Package {|
                        Source = reference
                        Spdx = metaOverride.SpdxExpression
                        Copyright = metaOverride.Copyright
                    |}
                | false, _ ->
                    task {
                        let! nuSpec = nuGet.ReadNuSpec reference
                        return GetMetadata reference nuSpec
                    }

            result.Add metadata

        return {
            Items = result.AsReadOnly()
            UsedOverrides = usedOverrides
        }
    }
