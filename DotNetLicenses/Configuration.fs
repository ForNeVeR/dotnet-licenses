// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses

open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open DotNetLicenses.CommandLine
open TruePath
open Tomlyn
open Tomlyn.Model

#nowarn "3511"

type Configuration =
    {
        MetadataSources: MetadataSource[]
        MetadataOverrides: Override[]
        LockFile: LocalPath option
        PackagedFiles: PackageSpec[]
    }

    static member Empty = {
        MetadataSources = Array.empty
        MetadataOverrides = Array.empty
        LockFile = None
        PackagedFiles = Array.empty
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
            let readItemOrArray key transform t =
                let value: obj option = tryGetValue t key
                match value with
                | None -> Array.empty
                | Some(:? string as s) -> [| transform s |]
                | Some(:? TomlArray as a) -> a |> Seq.cast<string> |> Seq.map transform |> Seq.toArray
                | Some other -> failwithf $"{key}'s type not supported: {other}."

            let readFilesCovered = readItemOrArray "files_covered" LocalPathPattern

            array
            |> Seq.cast<TomlTable>
            |> Seq.map(fun t ->
                match getValue t "type" with
                | "nuget" -> NuGet { Include = getValue t "include" |> LocalPath }
                | "license" ->
                    License {
                        Spdx = getValue t "spdx"
                        Copyright = getValue t "copyright"
                        FilesCovered = readFilesCovered t
                    }
                | "reuse" ->
                    Reuse {
                        Root = getValue t "root" |> LocalPath
                        Exclude = readItemOrArray "exclude" (fun x -> LocalPath x) t
                        FilesCovered = readFilesCovered t
                    }
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
            | "directory" -> Directory(getValue t "path" |> LocalPath)
            | "zip" -> Zip(getValue t "path" |> LocalPathPattern)
            | other -> failwithf $"Packaged file source type not supported: {other}"
        )

        use reader = new StreamReader(stream)
        let! text = reader.ReadToEndAsync()
        let table = Toml.ToModel(text, sourcePath = Option.toObj filePath)

        let sources = getValue table "metadata_sources"
        let overrides = tryGetValue table "metadata_overrides"
        let lockFilePath = tryGetValue table "lock_file" |> Option.map LocalPath
        let packagedFiles = tryGetValue table "packaged_files"

        return {
            MetadataSources = readSources sources
            MetadataOverrides = readOverrides overrides
            LockFile = lockFilePath
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
        let packageReference(o: Override) = {
            PackageId = o.Id
            Version = o.Version
        }
        let metadataOverride(o: Override) = {
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

and MetadataSource =
    | NuGet of NuGetSource
    | License of LicenseSource
    | Reuse of ReuseSource

and Override = {
    Id: string
    Version: string
    Spdx: string
    Copyright: string
}

and PackageSpec =
    | Directory of path: LocalPath
    | Zip of path: LocalPathPattern

and AssignedMetadata = {
    Files: LocalPathPattern
    MetadataSourceId: string
}

and NuGetSource =
    {
        Include: LocalPath
    }
    static member Of(path: LocalPath) = NuGet { Include = path }
    static member Of(relativePath: string) = NuGetSource.Of(LocalPath relativePath)

and LicenseSource = {
    Spdx: string
    Copyright: string
    FilesCovered: LocalPathPattern[]
}

and ReuseSource =
    {
        Root: LocalPath
        Exclude: LocalPath[]
        FilesCovered: LocalPathPattern[]
    }
    static member Of(path: LocalPath) = Reuse {
        Root = path
        Exclude = Array.empty
        FilesCovered = Array.empty
    }
    static member Of(relativePath: string) = ReuseSource.Of(LocalPath relativePath)
