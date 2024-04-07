// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Sources

open System.Collections.Generic
open System.IO
open System.Threading.Tasks

type SourceEntry = {
    Source: PackageSpec
    Path: string
}

let private ExtractFileEntries(spec: PackageSpec) = task {
    do! Task.Yield()
    let options = EnumerationOptions(RecurseSubdirectories = true, IgnoreInaccessible = false)
    return
        Directory.EnumerateFileSystemEntries(spec.Path, "*", options)
        |> Seq.map(fun path -> { Source = spec; Path = path })
        |> Seq.toArray
}

let private ExtractEntries(spec: PackageSpec) = task {
    return!
        match spec.Type with
        | "file" -> ExtractFileEntries spec
        | other -> failwithf $"Package spec type not supported: {other}."
}

let ReadEntries(spec: PackageSpec seq): Task<IReadOnlyList<SourceEntry>> = task {
    let result = ResizeArray()
    for package in spec do
        let! entries = ExtractEntries package
        result.AddRange entries
    return result
}
