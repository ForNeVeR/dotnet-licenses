// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Ignores

open System.Collections.Immutable
open Microsoft.Extensions.FileSystemGlobbing
open TruePath

let Licenses = Preset "licenses"

let DefaultPresets: IgnorePattern[] = [|
    Licenses
|]

let AllPresets = Map.ofArray [|
    "licenses", LocalPathPattern "LICENSES/*.txt"
|]

let Filter(patterns: IgnorePattern seq) (items: ISourceEntry seq): PackagedEntries =
    let getFilter = function
        | Preset name -> Map.find name AllPresets

    let itemsByPath =
        items
        |> Seq.groupBy _.SourceRelativePath

    let patterns = patterns |> Seq.map getFilter
    let ignoreMatcher = Matcher()
    ignoreMatcher.AddIncludePatterns(patterns |> Seq.map _.Value)

    let sourceEntries = ResizeArray()
    let ignoredEntries = ResizeArray()
    for path, item in itemsByPath do
        if ignoreMatcher.Match(path).HasMatches then
            ignoredEntries.AddRange item
        else
            sourceEntries.AddRange item

    {
        SourceEntries = ImmutableArray.ToImmutableArray sourceEntries
        IgnoredEntries = ImmutableArray.ToImmutableArray ignoredEntries
    }
