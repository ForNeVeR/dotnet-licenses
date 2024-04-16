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

let SaveLockFile(path: AbsolutePath, items: SortedDictionary<string, IReadOnlyList<LockFileItem>>): Task =
    // Dictionary is important, since Tomlyn casts to raw IDictionary under the cover, which is not supported by F# Map
    let text = Toml.FromModel items
    File.WriteAllTextAsync(path.Value, text)
