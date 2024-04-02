// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses

open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open JetBrains.Annotations
open Tomlyn

[<CLIMutable>]
type Configuration =
    {
        Inputs: string[]
        [<CanBeNull>] Overrides: Override[]
    }

    static member Read(stream: Stream, filePath: string option): Task<Configuration> = task {
        use reader = new StreamReader(stream)
        let! text = reader.ReadToEndAsync()
        return Toml.ToModel<Configuration>(text, Option.toObj filePath, options = null)
    }

    static member ReadFromFile(path: string): Task<Configuration> = task {
        use stream = new FileStream(path, FileMode.Open, FileAccess.Read)
        return! Configuration.Read(stream, Some path)
    }

    member this.GetOverrides(): IReadOnlyDictionary<PackageReference, MetadataOverride> =
        let packageReference o = {
            PackageId = o.Id
            Version = o.Version
        }
        let metadataOverride o = {
            SpdxExpression = o.Spdx
            Copyright = o.Copyright
        }

        let map = Dictionary()
        Option.ofObj this.Overrides
        |> Option.defaultValue Array.empty
        |> Seq.map(fun o -> packageReference o, metadataOverride o)
        |> Seq.iter(fun (k, v) ->
            if not <| map.TryAdd(k, v) then
                failwithf $"Duplicate key {k.PackageId} in the overrides collection."
        )
        map

and [<CLIMutable>] Override =
    {
        Id: string
        Version: string
        Spdx: string
        Copyright: string
    }
