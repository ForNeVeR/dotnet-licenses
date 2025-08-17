module DotNetLicenses.GeneratedArtifacts

open System
open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open Microsoft.Extensions.FileSystemGlobbing
open TruePath

[<RequireQualifiedAccess>]
type GeneratedArtifact =
    | File of AbsolutePath
    | Pattern of LocalPathPattern

type GeneratedArtifactEntry = {
    Artifact: GeneratedArtifact
    AdditionalLicense: ILicense option
}

let Merge(items: GeneratedArtifactEntry seq): GeneratedArtifactEntry seq =
    items
    |> Seq.groupBy _.Artifact
    |> Seq.map (fun (k, v) ->
        let license = v |> Seq.choose _.AdditionalLicense |> Seq.toArray |> ILicense.Merge
        {
            Artifact = k
            AdditionalLicense = license
        }
    )

let Split(entries: GeneratedArtifactEntry seq): IList<AbsolutePath> * IList<LocalPathPattern> =
    let files = ResizeArray()
    let patterns = ResizeArray()
    for entry in entries do
        match entry.Artifact with
        | GeneratedArtifact.File f -> files.Add f
        | GeneratedArtifact.Pattern p -> patterns.Add p

    files, patterns

type CoverageResult = bool * ILicense seq

let CalculateCoverage (hashCache: FileHashCache)
                      (sourceEntry: ISourceEntry)
                      (artifacts: GeneratedArtifactEntry seq): Task<CoverageResult> = task {
    let files = ResizeArray()
    let mutable anyMatch = false
    let licenses = ResizeArray()

    for artifact in artifacts do
        match artifact.Artifact with
        | GeneratedArtifact.File f -> files.Add(f, artifact.AdditionalLicense)
        | GeneratedArtifact.Pattern p ->
            let matcher = Matcher().AddInclude(p.Value)
            if matcher.Match(sourceEntry.SourceRelativePath).HasMatches then
                anyMatch <- true
                artifact.AdditionalLicense |> Option.iter licenses.Add

    let! fileHash = sourceEntry.CalculateHash()
    let! hashes =
        files
        |> Seq.filter (fun(path, _) -> File.Exists(path.Value))
        |> Seq.map(fun(path, license) -> task {
            let! hash = hashCache.CalculateFileHash path
            return hash, license
        })
        |> Task.WhenAll
    for hash, license in hashes do
        if hash.Equals(fileHash, StringComparison.OrdinalIgnoreCase) then
            anyMatch <- true
            license |> Option.iter licenses.Add

    return anyMatch, licenses :> _ seq
}
