// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.LockFile

open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open JetBrains.Annotations
open TruePath
open Tomlyn

[<CLIMutable>]
type LockFileItem = {
    [<CanBeNull>] SourceId: string
    [<CanBeNull>] SourceVersion: string
    Spdx: string[]
    Copyright: string[]
}

type LockFileEntry = LocalPathPattern * LockFileItem

let SaveLockFile(path: AbsolutePath, items: LockFileEntry seq): Task =
    let dictionary = SortedDictionary<string, ResizeArray<LockFileItem>>()
    for pattern, item in items do
        match dictionary.TryGetValue pattern.Value with
        | true, list -> list.Add item
        | false, _ ->
            let list = ResizeArray()
            list.Add item
            dictionary.Add(pattern.Value, list)

    let text = Toml.FromModel dictionary
    File.WriteAllTextAsync(path.Value, text)
