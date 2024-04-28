// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.CoveragePattern

open System
open System.Collections.Concurrent
open System.Threading.Tasks
open DotNetLicenses.LockFile
open DotNetLicenses.Metadata
open Microsoft.Extensions.FileSystemGlobbing
open ReuseSpec
open TruePath

type CoverageCache =
    // Project path => Project output path
    | CoverageCache of ConcurrentDictionary<AbsolutePath, Task<AbsolutePath>>

    static member Empty(): CoverageCache = CoverageCache(ConcurrentDictionary())
    member this.Read(project: AbsolutePath) =
        let (CoverageCache map) = this
        map.GetOrAdd(project, MsBuild.GetProjectGeneratedArtifacts)

let private CollectLockFileItem (baseDir: AbsolutePath) = function
    | Package _ -> failwith "Function doesn't support packages."
    | License source ->
        Task.FromResult {
            SourceId = null
            SourceVersion = null
            Spdx = [|source.Spdx|]
            Copyright = [|source.Copyright|]
        }
    | Reuse source -> task {
        let basePath = baseDir / source.Root
        let! entries = Reuse.ReadReuseDirectory(basePath, source.Exclude |> Seq.map(fun x -> basePath / x))
        let resultEntry = ReuseFileEntry.CombineEntries(basePath, entries)
        return {
            SourceId = null
            SourceVersion = null
            Spdx = Seq.toArray resultEntry.LicenseIdentifiers
            Copyright = Seq.toArray resultEntry.CopyrightStatements
        }
    }

#nowarn "3511"

let private NuGetCoverageGlob =
    let glob(x: string) =
        let matcher = Matcher()
        matcher.AddIncludePatterns [| x |]
        x, matcher
    [|
        glob "[Content_Types].xml"
        glob "_rels/.rels"
        glob "package/services/metadata/core-properties/*.psmdcp"
    |] |> Map.ofSeq

let CollectCoveredFileLicense (baseDirectory: AbsolutePath)
                              (coverageCache: CoverageCache)
                              (hashCache: FileHashCache)
                              (metadataItems: MetadataItem seq)
                              (sourceEntry: ISourceEntry): Task<ResizeArray<LocalPathPattern * LockFileItem>> = task {
    let doesSpecCover spec =
        match spec with
        | MsBuildCoverage project ->
            task {
                let! projectOutput = coverageCache.Read(baseDirectory / project)
                let! fileHash = sourceEntry.CalculateHash()
                let! outputHash = hashCache.CalculateFileHash projectOutput
                return fileHash.Equals(outputHash, StringComparison.OrdinalIgnoreCase)
            }
        | NuGetCoverage ->
            Task.FromResult(
                NuGetCoverageGlob.Values
                |> Seq.exists _.Match(sourceEntry.SourceRelativePath).HasMatches
            )

    let getLockFileItem source spec =
        match spec with
        | MsBuildCoverage _ ->
            task {
                let! item = CollectLockFileItem baseDirectory source
                return LocalPathPattern sourceEntry.SourceRelativePath, item
            }
        | NuGetCoverage -> task {
            let pattern =
                (
                    NuGetCoverageGlob
                    |> Seq.filter(_.Value.Match(sourceEntry.SourceRelativePath).HasMatches)
                    |> Seq.head
                ).Key
            let! item = CollectLockFileItem baseDirectory source
            return LocalPathPattern pattern, item
        }

    let patterns =
        metadataItems
        |> Seq.map(fun source ->
            source,
            match source with
            | Package _ -> Seq.empty
            | License source -> source.PatternsCovered
            | Reuse source -> source.PatternsCovered
        )

    let result = ResizeArray()
    for source, specs in patterns do
        for spec in specs do
            let! isCoveredBySpec = doesSpecCover spec
            if isCoveredBySpec then
                let! lockEntry = getLockFileItem source spec
                result.Add lockEntry

    return result
}
