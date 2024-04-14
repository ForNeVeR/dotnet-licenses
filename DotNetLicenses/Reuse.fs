// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Reuse

open System
open System.Collections.Immutable
open System.IO
open System.Threading.Tasks
open DotNetLicenses.Sources
open Microsoft.Extensions.FileSystemGlobbing
open ReuseSpec
open TruePath

type ReuseLicenseEntry = {
    SpdxExpressions: ImmutableArray<string>
    CopyrightStatements: ImmutableArray<string>
}

let ReadReuseDirectory(root: AbsolutePath, excludes: AbsolutePath seq): Task<ResizeArray<ReuseFileEntry>> = task {
    let! entries = ReuseDirectory.ReadEntries root
    let result = ResizeArray()
    let matcher = Matcher().AddInclude "*"
    matcher.AddExcludePatterns(excludes |> Seq.map _.Value)
    for entry in entries do
        if not <| matcher.Match(entry.Path.Value).HasMatches then
            result.Add entry
    return result
}

let CollectLicenses (cache: FileHashCache)
                    (reuseEntries: ReuseFileEntry seq)
                    (sourceEntry: ISourceEntry): Task<ResizeArray<ReuseLicenseEntry>> = task {
    let! fileHash = sourceEntry.CalculateHash()
    let result = ResizeArray()
    let similarEntries =
        reuseEntries
        |> Seq.filter (fun reuseEntry -> reuseEntry.Path.FileName = Path.GetFileName sourceEntry.SourceRelativePath)
    for reuseEntry in similarEntries do
        let! reuseHash = cache.CalculateFileHash reuseEntry.Path
        if fileHash.Equals(reuseHash, StringComparison.OrdinalIgnoreCase) then
            result.Add {
                SpdxExpressions = reuseEntry.LicenseIdentifiers
                CopyrightStatements = reuseEntry.CopyrightStatements
            }

    return result
}
