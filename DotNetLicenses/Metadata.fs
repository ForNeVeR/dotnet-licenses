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
    | Package of {| Source: PackageReference; Spdx: string[]; Copyrights: string[] |}
    | License of LicenseSource
    | Reuse of ReuseSource

let internal GetMetadata (source: PackageReference) (nuSpec: NuSpec): MetadataItem option =
    let metadata = nuSpec.Metadata
    let license = metadata.License
    if isNull <| box license then
        None
    else

    let spdx =
        match license.Type with
        | "expression" -> metadata.License.Value
        | "file" -> $"<No SPDX Expression; see https://www.nuget.org/packages/{source.PackageId}/{source.Version}/License>"
            // TODO: ↑ Support the files properly?
        | _ -> failwithf $"Unsupported license type for source {source.PackageId} v{source.Version}: {license.Type}"
    Some <| Package {|
        Source = source
        Spdx = [|spdx|]
        Copyrights = [|metadata.Copyright|]
    |}

type MetadataReadResult = {
    Items: ImmutableArray<MetadataItem>
    UsedOverrides: Set<PackageReference>
}

type MetadataReader(nuGet: INuGetReader) =
    member _.ReadFromProject(
        projectFilePath: AbsolutePath,
        overrides: IReadOnlyDictionary<PackageReference, MetadataOverride>
    ): Task<MetadataReadResult> = task {
        let mutable usedOverrides = Set.empty

        let! packageReferences = ReadTransitiveProjectReferences projectFilePath
        let result = ResizeArray(packageReferences.Length)
        for reference in packageReferences do
            let! metadata =
                match overrides.TryGetValue reference with
                | true, metaOverride ->
                    usedOverrides <- usedOverrides |> Set.add reference
                    Task.FromResult(Some <| Package {|
                        Source = reference
                        Spdx = metaOverride.SpdxExpressions
                        Copyrights = metaOverride.Copyrights
                    |})
                | false, _ ->
                    task {
                        let! nuSpec = nuGet.ReadNuSpec reference
                        return GetMetadata reference nuSpec
                    }

            metadata |> Option.iter result.Add

        return {
            Items = result |> Seq.distinct |> ImmutableArray.ToImmutableArray
            UsedOverrides = usedOverrides
        }
    }
