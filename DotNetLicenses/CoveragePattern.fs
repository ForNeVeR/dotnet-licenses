// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.CoveragePattern

open System
open System.Collections.Concurrent
open System.Collections.Immutable
open System.IO
open System.Threading.Tasks
open DotNetLicenses.GeneratedArtifacts
open DotNetLicenses.LockFile
open DotNetLicenses.Metadata
open DotNetLicenses.MsBuild
open Microsoft.Extensions.FileSystemGlobbing
open ReuseSpec
open TruePath

type CoverageCache =
    // Project path => Project output path
    | CoverageCache of ConcurrentDictionary<AbsolutePath * string option, Task<GeneratedArtifactEntry[]>>

    static member Empty(): CoverageCache = CoverageCache(ConcurrentDictionary())
    member this.Read(project: AbsolutePath, runtime: string option) =
        let (CoverageCache map) = this
        map.GetOrAdd((project, runtime), GetGeneratedArtifacts)

let private CollectLockFileItem (baseDir: AbsolutePath) = function
    | Package _ -> failwith "Function doesn't support packages."
    | License source ->
        Task.FromResult {
            LockFileItem.SourceId = None
            SourceVersion = None
            SpdxExpression = Some source.SpdxExpression
            CopyrightNotices = ImmutableArray.Create source.CopyrightNotice
            IsIgnored = false
        }
    | Reuse source -> task {
        let basePath = baseDir / source.Root
        let! entries = Reuse.ReadReuseDirectory(basePath, source.Exclude |> Seq.map(fun x -> basePath / x))
        let resultEntry = ReuseFileEntry.CombineEntries(basePath, entries)
        return {
            LockFileItem.SourceId = None
            SourceVersion = None
            SpdxExpression = Some <| String.Join(" AND ", resultEntry.SpdxLicenseIdentifiers)
            CopyrightNotices = resultEntry.CopyrightNotices
            IsIgnored = false
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
        glob "*.nuspec"
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
        | MsBuildCoverage(project, runtime) ->
            task {
                let! projectOutputs = coverageCache.Read(baseDirectory / project, runtime)
                let files, patterns = GeneratedArtifacts.Split projectOutputs

                let matcher = Matcher()
                matcher.AddIncludePatterns(patterns |> Seq.map _.Value)
                if matcher.Match(sourceEntry.SourceRelativePath).HasMatches then
                    return true
                else

                let! fileHash = sourceEntry.CalculateHash()
                let! hashes =
                    files
                    |> Seq.filter (fun p -> File.Exists(p.Value))
                    |> Seq.map hashCache.CalculateFileHash
                    |> Task.WhenAll
                return hashes |> Seq.exists _.Equals(fileHash, StringComparison.OrdinalIgnoreCase)
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
