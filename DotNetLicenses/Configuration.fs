// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses

open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open DotNetLicenses.CommandLine
open Tomlyn
open Tomlyn.Model

type Configuration =
    {
        MetadataSources: MetadataSource[]
        MetadataOverrides: Override[] option
        LockFilePath: string option
        PackagedFiles: PackageSpec[] option
    }

    static member Empty = {
        MetadataSources = Array.empty
        MetadataOverrides = None
        LockFilePath = None
        PackagedFiles = None
    }

    static member Read(stream: Stream, filePath: string option): Task<Configuration> = task {
        let readSources table = failwith "TODO"
        let readOverrides = failwith "TODO"
        let readPackagedFiles = failwith "TODO"

        use reader = new StreamReader(stream)
        let! text = reader.ReadToEndAsync()
        let table = Toml.ToModel(text, sourcePath = Option.toObj filePath)

        let sources = table["metadata_sources"] :?> TomlArray
        let overrides = table["metadata_overrides"] :?> TomlArray
        let lockFilePath = table["lock_file"] :?> string
        let packagedFiles = table["packaged_files"] :?> TomlArray

        return {
            MetadataSources = readSources sources
            MetadataOverrides = readOverrides overrides
            LockFilePath = Option.ofObj lockFilePath
            PackagedFiles = readPackagedFiles packagedFiles
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
        |> Option.defaultValue Array.empty
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

and MetadataSource =
    NuGet of ``include``: string

and Override = {
    Id: string
    Version: string
    Spdx: string
    Copyright: string
}

and PackageSpec =
    | Directory of path: string
    | Zip of path: string
