// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Metadata

open System.Collections.Generic
open System.Collections.Immutable
open System.Threading.Tasks
open DotNetLicenses.NuGet
open TruePath

type MetadataItem =
    | Package of {| Source: PackageReference; License: ILicense |}
    | License of LicenseSource
    | Reuse of ReuseSource

let internal GetMetadata (input: AbsolutePath)
                         (nuGet: INuGetReader)
                         (reference: PackageReference): Task<MetadataItem option> = task {
    let coords = reference.Coordinates
    let! nuSpec = nuGet.ReadNuSpec input coords
    let license =
        nuSpec
        |> Option.bind (fun nuSpec ->
            let metadata = nuSpec.Metadata
            let license = metadata.License
            if isNull <| box license then
                None
            else

            let packageId, version = coords.PackageId, coords.Version
            let spdx =
                match license.Type with
                | "expression" -> metadata.License.Value
                | "file" -> $"<No SPDX Expression; see https://www.nuget.org/packages/{packageId}/{version}/License>"
                    // TODO[#57]: ↑ Support the files properly?
                | _ -> failwithf $"Unsupported license type for source {packageId} v{version}: {license.Type}"
            Some({
                SpdxExpression = spdx
                CopyrightNotices = ImmutableArray.Create metadata.Copyright
            } :> ILicense)
        )

    return license |> Option.map (fun l -> Package {|
        Source = reference
        License = l
    |})
}

type MetadataReadResult = {
    Items: ImmutableArray<MetadataItem>
    UsedOverrides: Set<PackageCoordinates>
}

type MetadataReader(nuGet: INuGetReader) =
    member _.ReadFromProject(
        projectFilePath: AbsolutePath,
        overrides: IReadOnlyDictionary<PackageCoordinates, MetadataOverride>
    ): Task<MetadataReadResult> = task {
        let mutable usedOverrides = Set.empty

        let! packageReferences = nuGet.ReadPackageReferences projectFilePath
        let result = ResizeArray(packageReferences.Length)
        for reference in packageReferences do
            let coordinates = reference.Coordinates
            let! metadata =
                match overrides.TryGetValue coordinates with
                | true, metaOverride ->
                    usedOverrides <- usedOverrides |> Set.add coordinates
                    Task.FromResult(Some <| Package {|
                        Source = reference
                        License = {
                            SpdxLicense.SpdxExpression = metaOverride.SpdxExpression
                            CopyrightNotices = ImmutableArray.ToImmutableArray metaOverride.CopyrightNotices
                        }
                    |})
                | false, _ -> GetMetadata projectFilePath nuGet reference

            metadata |> Option.iter result.Add

        return {
            Items = result |> Seq.distinct |> ImmutableArray.ToImmutableArray
            UsedOverrides = usedOverrides
        }
    }
