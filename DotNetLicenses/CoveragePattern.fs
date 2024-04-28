// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.CoveragePattern

open System
open System.Collections.Concurrent
open System.IO
open System.Threading.Tasks
open DotNetLicenses.LockFile
open DotNetLicenses.Metadata
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

let CollectCoveredFileLicense (baseDirectory: AbsolutePath)
                              (coverageCache: CoverageCache)
                              (hashCache: FileHashCache)
                              (metadataItems: MetadataItem seq)
                              (sourceEntry: ISourceEntry): Task<LockFileItem seq> = task {
    let! projectOutputs =
        metadataItems
        |> Seq.map(fun source ->
            source,
            match source with
            | Package _ -> Seq.empty
            | License source -> source.PatternsCovered
            | Reuse source -> source.PatternsCovered
        )
        |> Seq.collect(fun (source, specs) ->
            specs
            |> Seq.map(fun (MsBuildCoverage input) -> task {
                let project = baseDirectory / input
                let! output = coverageCache.Read project
                return source, output
            })
        )
        |> Task.WhenAll

    let similarEntries =
        projectOutputs
        |> Seq.filter(fun (_, e) -> e.FileName = Path.GetFileName sourceEntry.SourceRelativePath)

    // TODO: Extract into a function ↓
    let result = ResizeArray()
    let! fileHash = sourceEntry.CalculateHash()
    for source, entry in similarEntries do
        let! reuseHash = hashCache.CalculateFileHash entry
        if fileHash.Equals(reuseHash, StringComparison.OrdinalIgnoreCase) then
            let! lockEntry = CollectLockFileItem baseDirectory source
            result.Add lockEntry

    return result :> LockFileItem seq
}
