// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses

open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open DotNetLicenses.CommandLine
open LocalPath
open Tomlyn
open Tomlyn.Model

type Configuration =
    {
        MetadataSources: MetadataSource[]
        MetadataOverrides: Override[]
        LockFilePath: string option
        PackagedFiles: PackageSpec[]
        AssignedMetadata: AssignedMetadata[]
    }

    static member Empty = {
        MetadataSources = Array.empty
        MetadataOverrides = Array.empty
        LockFilePath = None
        PackagedFiles = Array.empty
        AssignedMetadata = Array.empty
    }

    static member Read(stream: Stream, filePath: string option): Task<Configuration> = task {
        let tryGetValue (table: TomlTable) key =
            let value = ref Unchecked.defaultof<_>
            if table.TryGetValue(key, value)
            then Some(value.Value :?> 'a)
            else None

        let getValue (table: TomlTable) key =
            tryGetValue table key
            |> Option.defaultWith(fun () -> failwithf $"Key not found in table \"{table}\": \"{key}\".")

        let readSources(array: TomlArray) =
            array
            |> Seq.cast<TomlTable>
            |> Seq.map(fun t ->
                match getValue t "type" with
                | "nuget" -> NuGetSource {| Id = tryGetValue t "id"; Include = getValue t "include" |}
                | other -> failwithf $"Metadata source type not supported: {other}"
            )
            |> Seq.toArray

        let readOptArrayOfTablesUsing mapping input =
            input
            |> Option.map (fun array ->
                array
                |> Seq.cast<TomlTable>
                |> Seq.map mapping
                |> Seq.toArray
            )
            |> Option.defaultValue Array.empty

        let readOverrides = readOptArrayOfTablesUsing(fun t ->
            {
                Id = getValue t "id"
                Version = getValue t "version"
                Spdx = getValue t "spdx"
                Copyright = getValue t "copyright"
            }
        )

        let readPackagedFiles = readOptArrayOfTablesUsing(fun t ->
            match getValue t "type" with
            | "directory" -> Directory(getValue t "path")
            | "zip" -> Zip(getValue t "path")
            | other -> failwithf $"Packaged file source type not supported: {other}"
        )

        let readAssignedMetadata = readOptArrayOfTablesUsing(fun t ->
            {
                Files = LocalPathPattern(getValue t "files")
                MetadataSourceId = getValue t "metadata_source_id"
            }
        )

        use reader = new StreamReader(stream)
        let! text = reader.ReadToEndAsync()
        let table = Toml.ToModel(text, sourcePath = Option.toObj filePath)

        let sources = getValue table "metadata_sources"
        let overrides = tryGetValue table "metadata_overrides"
        let lockFilePath = tryGetValue table "lock_file"
        let packagedFiles = tryGetValue table "packaged_files"
        let assignedMetadata = tryGetValue table "assigned_metadata"

        return {
            MetadataSources = readSources sources
            MetadataOverrides = readOverrides overrides
            LockFilePath = lockFilePath
            PackagedFiles = readPackagedFiles packagedFiles
            AssignedMetadata = readAssignedMetadata assignedMetadata
        }
    }

    static member ReadFromFile(path: string): Task<Configuration> = task {
        use stream = new FileStream(path, FileMode.Open, FileAccess.Read)
        return! Configuration.Read(stream, Some path)
    }

    member this.GetMetadataOverrides(
        warningProcessor: WarningProcessor
    ): IReadOnlyDictionary<PackageReference, MetadataOverride> =
        let packageReference o = {
            PackageId = o.Id
            Version = o.Version
        }
        let metadataOverride o = {
            SpdxExpression = o.Spdx
            Copyright = o.Copyright
        }

        let map = Dictionary()
        this.MetadataOverrides
        |> Seq.map(fun o -> packageReference o, metadataOverride o)
        |> Seq.iter(fun (k, v) ->
            if not <| map.TryAdd(k, v) then
                warningProcessor.ProduceWarning(
                    ExitCode.DuplicateOverride,
                    $"Duplicate key in the metadata_overrides collection: id = \"{k.PackageId}\", version = \"{k.Version}\"."
                )
                map[k] <- v
        )
        map

    static member NuGet(``include``: string, ?id: string): MetadataSource =
        NuGetSource {| Id = id; Include = ``include`` |}

and MetadataSource =
    NuGetSource of {| Id: string option; Include: string |}

and Override = {
    Id: string
    Version: string
    Spdx: string
    Copyright: string
}

and PackageSpec =
    | Directory of path: string
    | Zip of path: string

and AssignedMetadata = {
    Files: LocalPathPattern
    MetadataSourceId: string
}
