// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Sources

open System.Collections.Generic
open System.IO
open System.Threading.Tasks

type SourceEntry =
    {
        Source: PackageSpec
        Path: string
    }
    member this.SourceRelativePath =
        match this.Source.Type with
        | "directory" -> Path.GetRelativePath(this.Source.Path, this.Path).Replace(Path.DirectorySeparatorChar, '/')
        | other -> failwithf $"Package spec type not supported: {other}."

    member _.CalculateHash(): Task<string> =
        failwith "TODO"

let private ExtractDirectoryEntries(spec: PackageSpec) = task {
    do! Task.Yield()
    let options = EnumerationOptions(RecurseSubdirectories = true, IgnoreInaccessible = false)
    return
        Directory.EnumerateFileSystemEntries(spec.Path, "*", options)
        |> Seq.map(fun path -> { Source = spec; Path = path })
        |> Seq.toArray
}

let private ExtractEntries(spec: PackageSpec) =
    match spec.Type with
    | "directory" -> ExtractDirectoryEntries spec
    | other -> failwithf $"Package spec type not supported: {other}."

let ReadEntries(spec: PackageSpec seq): Task<IReadOnlyList<SourceEntry>> = task {
    let result = ResizeArray()
    for package in spec do
        let! entries = ExtractEntries package
        result.AddRange entries
    return result
}
