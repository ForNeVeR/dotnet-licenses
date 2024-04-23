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
open TruePath
open Microsoft.Extensions.FileSystemGlobbing

type ISourceEntry =
    abstract member Source: PackageSpec
    abstract member SourceRelativePath: string
    abstract member ReadContent: unit -> Task<Stream>
    abstract member CalculateHash: unit -> Task<string>

type private HashCalculator(entry: ISourceEntry, filePathToCheck: AbsolutePath) =
    let mutable hashCalculationCache = None
    member _.CalculateHash(): Task<string> =
        let lastWriteTime = File.GetLastWriteTimeUtc filePathToCheck.Value
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

type internal FileSourceEntry(basePath: AbsolutePath, package: PackageSpec, fullPath: AbsolutePath) as this =
    let calculator = HashCalculator(this, fullPath)

    interface ISourceEntry with
        member _.Source = package

        member _.SourceRelativePath =
            match package.Source with
            | Directory path ->
                let sourcePath = basePath / path
                Path.GetRelativePath(sourcePath.Value, fullPath.Value).Replace(Path.DirectorySeparatorChar, '/')
            | other -> failwithf $"Package spec type not supported: {other}."

        member _.ReadContent(): Task<Stream> =
            Task.FromResult<Stream>(File.OpenRead fullPath.Value)

        member this.CalculateHash(): Task<string> = calculator.CalculateHash()

type internal ZipSourceEntry(
    source: PackageSpec,
    archive: ZipArchive,
    archivePath: AbsolutePath,
    name: string
) as this =
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

let private ExtractZipFileEntries(lifetime: Lifetime, spec: PackageSpec, archivePath: AbsolutePath) = task {
    do! Task.Yield()
    let stream = File.OpenRead archivePath.Value
    let archive = new ZipArchive(stream, ZipArchiveMode.Read)
    lifetime.AddDispose archive |> ignore
    return
        archive.Entries
        |> Seq.map(fun e -> ZipSourceEntry(spec, archive, archivePath, e.FullName) :> ISourceEntry)
        |> Seq.toArray
}

let private IncludesMetaCharacters(p: LocalPathPattern) =
    let s = p.Value
    s.Contains '*' || s.Contains '?'

let private ExtractEntries(lifetime: Lifetime, baseDirectory: AbsolutePath, spec: PackageSpec) = task {
    do! Task.Yield()

    let! entries = task {
        match spec.Source with
        | Directory relativePath ->
            let fullPath = baseDirectory / relativePath
            let options = EnumerationOptions(RecurseSubdirectories = true, IgnoreInaccessible = false)
            return
                Directory.EnumerateFileSystemEntries(fullPath.Value, "*", options)
                |> Seq.map(fun path -> FileSourceEntry(baseDirectory, spec, AbsolutePath path) :> ISourceEntry)
                |> Seq.toArray
        | Zip relativePath ->
            if IncludesMetaCharacters relativePath then
                let matcher = Matcher().AddInclude relativePath.Value
                let! entries =
                    matcher.GetResultsInFullPath baseDirectory.Value
                    |> Seq.map (fun matchedArchivePath -> task {
                        let stream = File.OpenRead matchedArchivePath
                        let archive = new ZipArchive(stream, ZipArchiveMode.Read)
                        lifetime.AddDispose archive |> ignore
                        return
                            archive.Entries
                            |> Seq.map(fun e ->
                                ZipSourceEntry(spec, archive, AbsolutePath matchedArchivePath, e.FullName) :> ISourceEntry
                            )
                            |> Seq.toArray
                    })
                    |> Task.WhenAll
                return entries |> Seq.concat |> Seq.toArray
            else
                let archivePath = baseDirectory / LocalPath relativePath.Value
                return! ExtractZipFileEntries(lifetime, spec, archivePath)
    }

    // TODO: apply ignores
    return entries
}

let ReadEntries (lifetime: Lifetime)
                (baseDirectory: AbsolutePath)
                (spec: PackageSpec seq): Task<IReadOnlyList<ISourceEntry>> = task {
    let result = ResizeArray()
    for package in spec do
        let! entries = ExtractEntries(lifetime, baseDirectory, package)
        result.AddRange entries
    return result
}
