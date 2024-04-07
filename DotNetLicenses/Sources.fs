// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Sources

open System
open System.Collections.Generic
open System.IO
open System.Security.Cryptography
open System.Threading
open System.Threading.Tasks

type SourceEntry(source: PackageSpec, fullPath: string) =
    do if not <| Path.IsPathRooted fullPath then
        failwithf $"Path is not absolute: \"{fullPath}\"."

    let mutable hashCalculationCache = None

    member _.Source = source
    member _.FullPath = fullPath

    member this.SourceRelativePath =
        match this.Source.Type with
        | "directory" -> Path.GetRelativePath(this.Source.Path, this.FullPath).Replace(Path.DirectorySeparatorChar, '/')
        | other -> failwithf $"Package spec type not supported: {other}."

    member this.ReadContent(): Task<Stream> =
        match this.Source.Type with
        | "directory" -> Task.FromResult<Stream>(File.OpenRead fullPath)
        | other -> failwithf $"Package spec type not supported: {other}."

    member this.CalculateHash(): Task<string> =
        let lastWriteTime = File.GetLastWriteTimeUtc fullPath
        match Volatile.Read(&hashCalculationCache) with
        | Some(cachedWriteTime, task) when cachedWriteTime >= lastWriteTime -> task
        | _ ->
            let calculationTask = task {
                use! content = this.ReadContent()
                use memory = new MemoryStream()
                do! content.CopyToAsync memory
                let data = memory.ToArray()
                use sha256 = SHA256.Create()
                let hash = sha256.ComputeHash(data)
                return Convert.ToHexString hash
            }
            Volatile.Write(&hashCalculationCache, Some(lastWriteTime, calculationTask))
            calculationTask

let private ExtractDirectoryEntries(spec: PackageSpec) = task {
    do! Task.Yield()
    let options = EnumerationOptions(RecurseSubdirectories = true, IgnoreInaccessible = false)
    return
        Directory.EnumerateFileSystemEntries(spec.Path, "*", options)
        |> Seq.map(fun path -> SourceEntry(spec, path))
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
