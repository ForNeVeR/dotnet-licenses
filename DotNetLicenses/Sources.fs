// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Sources

open System
open System.Collections.Generic
open System.IO
open System.IO.Compression
open System.Security.Cryptography
open System.Threading
open System.Threading.Tasks
open JetBrains.Lifetimes
open Microsoft.Extensions.FileSystemGlobbing

type ISourceEntry =
    abstract member Source: PackageSpec
    abstract member SourceRelativePath: string
    abstract member ReadContent: unit -> Task<Stream>
    abstract member CalculateHash: unit -> Task<string>

type private HashCalculator(entry: ISourceEntry, filePathToCheck: string) =
    let mutable hashCalculationCache = None
    member _.CalculateHash(): Task<string> =
        let lastWriteTime = File.GetLastWriteTimeUtc filePathToCheck
        match Volatile.Read(&hashCalculationCache) with
        | Some(cachedWriteTime, task) when cachedWriteTime >= lastWriteTime -> task
        | _ ->
            let calculationTask = task {
                use! content = entry.ReadContent()
                use memory = new MemoryStream()
                do! content.CopyToAsync memory
                let data = memory.ToArray()
                use sha256 = SHA256.Create()
                let hash = sha256.ComputeHash(data)
                return Convert.ToHexString hash
            }
            Volatile.Write(&hashCalculationCache, Some(lastWriteTime, calculationTask))
            calculationTask

type internal FileSourceEntry(source: PackageSpec, fullPath: string) as this =
    do if not <| Path.IsPathRooted fullPath then
        failwithf $"Path is not absolute: \"{fullPath}\"."

    let calculator = HashCalculator(this, fullPath)

    interface ISourceEntry with
        member _.Source = source

        member _.SourceRelativePath =
            match source with
            | Directory path -> Path.GetRelativePath(path, fullPath).Replace(Path.DirectorySeparatorChar, '/')
            | other -> failwithf $"Package spec type not supported: {other}."

        member _.ReadContent(): Task<Stream> =
            match source with
            | Directory path -> Task.FromResult<Stream>(File.OpenRead fullPath)
            | other -> failwithf $"Package spec type not supported: {other}."

        member this.CalculateHash(): Task<string> = calculator.CalculateHash()

type internal ZipSourceEntry(source: PackageSpec, archive: ZipArchive, archivePath: string, name: string) as this =
    let calculator = HashCalculator(this, archivePath)

    interface ISourceEntry with
        member _.Source = source
        member _.SourceRelativePath = name

        member _.ReadContent(): Task<Stream> =
            task {
                let entry = archive.GetEntry name
                if entry = null then
                    failwithf $"Entry not found: \"{name}\"."
                return entry.Open()
            }

        member this.CalculateHash(): Task<string> = calculator.CalculateHash()

let private ExtractZipFileEntries(lifetime: Lifetime, spec: PackageSpec, archivePath: string) = task {
    do! Task.Yield()
    let stream = File.OpenRead archivePath
    let archive = new ZipArchive(stream, ZipArchiveMode.Read)
    lifetime.AddDispose archive |> ignore
    return
        archive.Entries
        |> Seq.map(fun e -> ZipSourceEntry(spec, archive, archivePath, e.FullName) :> ISourceEntry)
        |> Seq.toArray
}

let private IncludesMetaCharacters(s: string) =
    s.Contains '*' || s.Contains '?'

let private ExtractEntries(lifetime: Lifetime, baseDirectory: string, spec: PackageSpec) = task {
    do! Task.Yield()

    match spec with
    | Directory relativePath ->
        let fullPath = Path.Combine(baseDirectory, relativePath)
        let options = EnumerationOptions(RecurseSubdirectories = true, IgnoreInaccessible = false)
        return
            Directory.EnumerateFileSystemEntries(fullPath, "*", options)
            |> Seq.map(fun path -> FileSourceEntry(spec, path) :> ISourceEntry)
            |> Seq.toArray
    | Zip relativePath ->
        if IncludesMetaCharacters relativePath then
            let matcher = Matcher().AddInclude relativePath
            let! entries =
                matcher.GetResultsInFullPath baseDirectory
                |> Seq.map (fun matchedArchivePath -> task {
                    let stream = File.OpenRead matchedArchivePath
                    let archive = new ZipArchive(stream, ZipArchiveMode.Read)
                    lifetime.AddDispose archive |> ignore
                    return
                        archive.Entries
                        |> Seq.map(fun e -> ZipSourceEntry(spec, archive, matchedArchivePath, e.FullName) :> ISourceEntry)
                        |> Seq.toArray
                })
                |> Task.WhenAll
            return entries |> Seq.concat |> Seq.toArray
        else
            let archivePath = Path.Combine(baseDirectory, relativePath)
            return! ExtractZipFileEntries(lifetime, spec, archivePath)
}

let ReadEntries (lifetime: Lifetime)
                (baseDirectory: string)
                (spec: PackageSpec seq): Task<IReadOnlyList<ISourceEntry>> = task {
    let result = ResizeArray()
    for package in spec do
        let! entries = ExtractEntries(lifetime, baseDirectory, package)
        result.AddRange entries
    return result
}
