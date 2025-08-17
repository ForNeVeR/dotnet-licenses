// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.CoveragePattern

open System
open System.Collections.Concurrent
open System.Collections.Immutable
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
    let specCoverage spec =
        match spec with
        | MsBuildCoverage(project, runtime) ->
            task {
                let! projectOutputs = coverageCache.Read(baseDirectory / project, runtime)
                return! CalculateCoverage hashCache sourceEntry projectOutputs
            }
        | NuGetCoverage ->
            let hasMatch =
                NuGetCoverageGlob.Values
                |> Seq.exists _.Match(sourceEntry.SourceRelativePath).HasMatches
            let additionalLicenses = Seq.empty<ILicense>
            Task.FromResult(hasMatch, additionalLicenses)

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
            let! isCoveredBySpec, additionalLicenses = specCoverage spec
            let additionalLicenses = Seq.toArray additionalLicenses

            if isCoveredBySpec then
                let! pattern, entry = getLockFileItem source spec
                let resultSpdx =
                    seq {
                        yield! Option.toArray entry.SpdxExpression
                        yield! additionalLicenses |> Seq.map(_.SpdxExpression)
                    } |> String.concat " AND "
                let resultCopyright =
                    entry.CopyrightNotices
                    |> Seq.append(additionalLicenses |> Seq.collect _.CopyrightNotices)
                    |> Seq.distinct
                    |> Seq.toArray
                let resultingEntry = {
                    entry with
                        SpdxExpression = if resultSpdx = "" then None else Some resultSpdx
                        CopyrightNotices = ImmutableArray.ToImmutableArray resultCopyright
                }
                result.Add(pattern, resultingEntry)

    return result
}
