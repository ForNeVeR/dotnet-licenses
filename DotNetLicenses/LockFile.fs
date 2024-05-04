// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.LockFile

open System
open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open JetBrains.Annotations
open TruePath
open Tomlyn

type LockFileItem = {
    SourceId: string option
    SourceVersion: string option
    Spdx: string[]
    Copyright: string[]
    IsIgnored: bool
}

[<CLIMutable>]
type StoredLockFileItem =
    {
        [<CanBeNull>] SourceId: string
        [<CanBeNull>] SourceVersion: string
        [<CanBeNull>] Spdx: string[]
        [<CanBeNull>] Copyright: string[]
        IsIgnored: Nullable<bool>
    }
    static member Create(item: LockFileItem): StoredLockFileItem =
        {
            SourceId = Option.toObj item.SourceId
            SourceVersion = Option.toObj item.SourceVersion
            Spdx = if item.IsIgnored && item.Spdx.Length = 0 then null else item.Spdx
            Copyright = if item.IsIgnored && item.Copyright.Length = 0 then null else item.Copyright
            IsIgnored = if item.IsIgnored then Nullable true else Nullable()
        }

    member this.FromStored(): LockFileItem =
        {
            SourceId = Option.ofObj this.SourceId
            SourceVersion = Option.ofObj this.SourceVersion
            Spdx = this.Spdx |> Option.ofObj |> Option.defaultValue Array.empty
            Copyright = this.Copyright |> Option.ofObj |> Option.defaultValue Array.empty
            IsIgnored = this.IsIgnored |> Option.ofNullable |> Option.defaultValue false
        }

type LockFileEntry = LocalPathPattern * LockFileItem

let SaveLockFile(path: AbsolutePath, items: LockFileEntry seq): Task =
    let dictionary = SortedDictionary<string, ResizeArray<StoredLockFileItem>>()
    for pattern, item in items do
        let item = StoredLockFileItem.Create item
        match dictionary.TryGetValue pattern.Value with
        | true, list -> list.Add item
        | false, _ ->
            let list = ResizeArray()
            list.Add item
            dictionary.Add(pattern.Value, list)

    let text = "# REUSE-IgnoreStart\n" + Toml.FromModel dictionary + "# REUSE-IgnoreEnd\n"
    File.WriteAllTextAsync(path.Value, text)

let ReadLockFile(path: AbsolutePath): Task<Dictionary<LocalPathPattern, LockFileItem[]>> = task {
    let! text = File.ReadAllTextAsync path.Value
    let dict = Toml.ToModel<Dictionary<string, StoredLockFileItem[]>>(text, sourcePath = path.Value)
    let result = Dictionary<LocalPathPattern, LockFileItem[]>()
    for pair in dict do
        let key = LocalPathPattern pair.Key
        result.Add(key, pair.Value |> Seq.map _.FromStored() |> Seq.toArray)
    return result
}
