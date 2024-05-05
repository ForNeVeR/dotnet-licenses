// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Processor

open System
open System.Collections.Generic
open System.Collections.Immutable
open System.IO
open System.Net.Http
open System.Reflection
open System.Threading.Tasks
open DotNetLicenses.CommandLine
open DotNetLicenses.CoveragePattern
open DotNetLicenses.LockFile
open DotNetLicenses.Metadata
open DotNetLicenses.NuGet
open DotNetLicenses.Reuse
open DotNetLicenses.Sources
open JetBrains.Lifetimes
open Microsoft.Extensions.FileSystemGlobbing
open Microsoft.FSharp.Core
open ReuseSpec
open TruePath

let private WarnUnusedOverrides allOverrides usedOverrides (wp: WarningProcessor) =
    let allOverrides = Set.ofSeq allOverrides
    let unusedOverrides = Set.difference allOverrides usedOverrides

    if not <| Set.isEmpty unusedOverrides then
        let stringOverrides =
            Seq.map (fun o -> $"id = \"{o.PackageId}\", version = \"{o.Version}\"") unusedOverrides
            |> String.concat ", "
        wp.ProduceWarning(ExitCode.UnusedOverride, $"Unused metadata overrides: {stringOverrides}.")

let private ProcessWarnings(wp: WarningProcessor) =
    for message in wp.Messages do
        eprintfn $"Warning: {message}"

    let exitCode =
        if wp.Codes.Count = 0
        then ExitCode.Success
        else Seq.max wp.Codes
    int exitCode

let private GetMetadata (projectMetadataReader: MetadataReader)
                        (baseFolderPath: AbsolutePath)
                        (overrides: IReadOnlyDictionary<PackageCoordinates, MetadataOverride>)
                        : MetadataSource -> Task<MetadataReadResult> = function
    | MetadataSource.NuGet source ->
        let projectPath = baseFolderPath / source.Include
        projectMetadataReader.ReadFromProject(projectPath, overrides)
    | MetadataSource.License source ->
        Task.FromResult {
            Items = ImmutableArray.Create(License source)
            UsedOverrides = Set.empty
        }
    | MetadataSource.Reuse source ->
        Task.FromResult {
            Items = ImmutableArray.Create(Reuse source)
            UsedOverrides = Set.empty
        }

let private CollectMetadata (config: Configuration) (baseFolderPath: AbsolutePath) nuGet wp = task {
    let reader = MetadataReader nuGet

    let overrides = config.GetMetadataOverrides wp
    let mutable usedOverrides = Set.empty

    let metadataList = ResizeArray()
    for source in config.MetadataSources do
        let! metadata = GetMetadata reader baseFolderPath overrides source
        usedOverrides <- Set.union usedOverrides metadata.UsedOverrides
        metadataList.AddRange metadata.Items

    WarnUnusedOverrides overrides.Keys usedOverrides wp
    return metadataList
}

let internal PrintMetadata(
    config: Configuration,
    baseFolderPath: AbsolutePath,
    nuGet: INuGetReader,
    wp: WarningProcessor
): Task = task {
    let! metadata = CollectMetadata config baseFolderPath nuGet wp
    for item in metadata do
        match item with
        | Package package ->
            let spdx = package.License.SpdxExpression
            let copyrights = package.License.CopyrightNotices |> String.concat "\n  "
            let coords =
                match package.Source with
                | NuGetReference c -> c
                | FrameworkReference c -> c
            printfn $"- Package {coords.PackageId} {coords.Version}: {spdx}\n  {copyrights}"

        | License license ->
            printfn $"- Manually defined license: {license.SpdxExpression}\n  {license.CopyrightNotice}"
            for pattern in license.FilesCovered do
                printfn $"  - {pattern}"
        | Reuse reuse ->
            printfn $"- REUSE-covered files from root directory \"{reuse.Root}\":"
            for pattern in reuse.Exclude do
                printfn $"  - Excluded pattern: \"{pattern}\""
            for pattern in reuse.FilesCovered do
                printfn $"  - Covered pattern: \"{pattern}\""
            let root = baseFolderPath / reuse.Root
            let excludes = reuse.Exclude |> Seq.map (fun e -> baseFolderPath / e)
            let! entries = ReadReuseDirectory(root, excludes)
            for entry in entries do
                let licenses = entry.SpdxLicenseIdentifiers |> String.concat ", "
                let copyrights = entry.CopyrightNotices |> String.concat "\n    "
                printfn $"  - {entry.Path}: {licenses}\n    {copyrights}"
}

let private IsCoveredByPattern (path: LocalPath) (pattern: LocalPathPattern) =
    let matcher = Matcher().AddInclude pattern.Value
    let result = matcher.Match path.Value
    result.HasMatches

#nowarn "3511"

let internal GenerateLockFile(
    config: Configuration,
    baseFolderPath: AbsolutePath,
    nuGet: INuGetReader,
    wp: WarningProcessor
): Task = task {
    match config.LockFile with
    | None -> wp.ProduceWarning(ExitCode.LockFileIsNotDefined, "lock_file is not specified in the configuration.")
    | Some lockFilePath ->

    match config.PackagedFiles with
    | [||] ->
        wp.ProduceWarning(
            ExitCode.PackagedFilesAreNotDefined,
            "packaged_files are not specified in the configuration."
        )
    | packagedFiles ->

    let lockFilePath = baseFolderPath / lockFilePath
    let! metadata = CollectMetadata config baseFolderPath nuGet wp
    let packages =
        metadata
        |> Seq.collect(function
            | Package p -> [| p.Source |]
            | _ -> Array.empty
        )
        |> Seq.toArray
    let licenses =
        metadata
        |> Seq.collect(function
            | License l -> [| l |]
            | _ -> Array.empty
        )
        |> Seq.toArray
    let packageMetadata =
        metadata
        |> Seq.collect(fun m ->
            match m with
            | Package p -> [| p.Source, m |]
            | _ -> Array.empty
        )
        |> Map.ofSeq
    let reuseMetadata =
        metadata
        |> Seq.collect(fun m ->
            match m with
            | Reuse r -> [| r |]
            | _ -> Array.empty
        )
        |> Seq.toArray
    let! reuseEntries = task {
        let sources =
            reuseMetadata
            |> Seq.map(fun r -> task {
                let root = baseFolderPath / r.Root
                let excludes = r.Exclude |> Seq.map (fun e -> baseFolderPath / e)
                let! entries = ReadReuseDirectory(root, excludes)
                return r, entries
            })
            |> Task.WhenAll
        let! sources = sources
        let dict = Dictionary()
        for r, s in sources do dict[r] <- s
        return dict
    }
    let allReuseEntries = reuseEntries.Values |> Seq.concat |> Seq.toArray

    use ld = new LifetimeDefinition()

    let! packagedEntries = ReadEntries ld.Lifetime baseFolderPath packagedFiles

    let findExplicitLicenses(entry: ISourceEntry) =
        licenses
        |> Seq.filter(fun license ->
            license.FilesCovered
            |> Seq.exists(fun pattern -> IsCoveredByPattern (LocalPath entry.SourceRelativePath) pattern)
        )
    use hashCache = new FileHashCache()
    let findReuseLicenses(entry: ISourceEntry) =
        CollectLicenses hashCache allReuseEntries entry
    let findReuseCombinedLicenses(entry: ISourceEntry) =
        reuseMetadata
        |> Seq.filter(fun r ->
            let matcher = Matcher()
            let patterns = r.FilesCovered |> Seq.map _.Value
            matcher.AddIncludePatterns(patterns)
            matcher.Match(entry.SourceRelativePath).HasMatches
        )
        |> Seq.map (fun r ->
            ReuseFileEntry.CombineEntries(baseFolderPath, reuseEntries[r])
        )

    let coverageCache = CoverageCache.Empty()

    let findLicensesForFile (entry: ISourceEntry): Task<LockFileEntry[]> = task {
        let! possiblePackageEntries =
            packages
            |> Seq.map(fun p ->
                nuGet.ContainsFileName p <| Path.GetFileName(entry.SourceRelativePath)
            )
            |> Task.WhenAll
        let fileContainingPackagesCount = possiblePackageEntries |> Seq.filter id |> Seq.length
        let! packageEntries = nuGet.FindFile hashCache packages entry
        let packageSearchResults =
            packageEntries
            |> Seq.map (fun package ->
                let metadata = packageMetadata[package]
                match metadata with
                | Package p ->
                    let coords = match package with NuGetReference c -> c | FrameworkReference c -> c
                    let sourceVersion = if fileContainingPackagesCount <= 1 then None else Some coords.Version
                    LocalPathPattern entry.SourceRelativePath, {
                        LockFileItem.SourceId = Some coords.PackageId
                        SourceVersion = sourceVersion
                        SpdxExpression = Some p.License.SpdxExpression
                        CopyrightNotices = p.License.CopyrightNotices
                        IsIgnored = false
                    }
                | _ -> failwithf $"Unexpected metadata type in object {metadata}."
            )
        let licenseSearchResults =
            findExplicitLicenses entry
            |> Seq.map (fun license -> LocalPathPattern entry.SourceRelativePath, {
                LockFileItem.SourceId = None
                SourceVersion = None
                SpdxExpression = Some license.SpdxExpression
                CopyrightNotices = ImmutableArray.Create license.CopyrightNotice
                IsIgnored = false
            })
        let! reuseEntries = findReuseLicenses entry
        let reuseSearchResults =
            reuseEntries
            |> Seq.map (fun reuseEntry -> LocalPathPattern entry.SourceRelativePath, {
                LockFileItem.SourceId = None
                SourceVersion = None
                SpdxExpression = Some reuseEntry.SpdxExpression
                CopyrightNotices = reuseEntry.CopyrightStatements
                IsIgnored = false
            })
        let reuseCombinedSearchResults =
            findReuseCombinedLicenses entry
            |> Seq.map(fun combinedEntry -> LocalPathPattern entry.SourceRelativePath, {
                LockFileItem.SourceId = None
                SourceVersion = None
                SpdxExpression = Some <| String.Join(" AND ", combinedEntry.SpdxLicenseIdentifiers)
                CopyrightNotices = combinedEntry.CopyrightNotices
                IsIgnored = false
            })
        let! coveragePatternSearchResults =
            CollectCoveredFileLicense baseFolderPath coverageCache hashCache metadata entry
        return Seq.concat [|
            packageSearchResults
            licenseSearchResults
            reuseSearchResults
            reuseCombinedSearchResults
            coveragePatternSearchResults
        |] |> Seq.toArray
    }

    let lockFileContent = ResizeArray<LockFileEntry>()
    for entry in packagedEntries.SourceEntries do
        let! resultEntries = findLicensesForFile entry
        match resultEntries.Length with
        | 0 ->
            wp.ProduceWarning(
                ExitCode.LicenseForFileNotFound,
                $"No license found for file \"{entry.SourceRelativePath}\"."
            )
        | 1 -> lockFileContent.AddRange resultEntries
        | _ ->
            let expressions =
                resultEntries
                |> Seq.distinctBy(fun(_, x) -> x.Spdx)
                |> Seq.toArray
            if expressions.Length > 1 then
                let differentExpressions =
                    expressions
                    |> Seq.map(fun (_, entry: LockFileItem) ->
                        String.concat "" [|
                            "["
                            entry.Spdx |> String.concat ", "
                            "]"
                            match entry.SourceId, entry.SourceVersion with
                            | sourceId, Some version ->
                                let id = sourceId |> Option.defaultValue "unknown source"
                                $" ({id} {version})"
                            | Some id, None -> $" ({id})"
                            | None, None -> ()
                        |]
                    )
                    |> String.concat ", "
                wp.ProduceWarning(
                    ExitCode.DifferentLicenseSetsForFile,
                    $"Multiple different license sets for file \"{entry.SourceRelativePath}\": {differentExpressions}."
                )
            lockFileContent.AddRange resultEntries

    for entry in packagedEntries.IgnoredEntries do
        lockFileContent.Add(
            LocalPathPattern entry.SourceRelativePath,
            {
                LockFileItem.SourceId = None
                SourceVersion = None
                SpdxExpression = None
                CopyrightNotices = ImmutableArray.Create()
                IsIgnored = true
            }
        )

    do! SaveLockFile(lockFilePath, lockFileContent)
}

let Verify(config: Configuration, baseFolder: AbsolutePath, wp: WarningProcessor): Task = task {
    match config.LockFile with
    | None -> wp.ProduceWarning(ExitCode.LockFileIsNotDefined, "lock_file is not specified in the configuration.")
    | Some lockFile ->

    let! lockFileEntries = ReadLockFile(baseFolder / lockFile)
    for entry in lockFileEntries do
        let pattern = entry.Key
        let items = entry.Value
        for item in items do
            if item.IsIgnored && (Option.isSome item.SpdxExpression || item.CopyrightNotices.Length > 0) then
                wp.ProduceWarning(
                    ExitCode.NonEmptyIgnoredLockFileEntry,
                    $"Ignored entry with license or copyright set for path \"{pattern}\"."
                )
            else if not item.IsIgnored && Option.isNone item.SpdxExpression then
                wp.ProduceWarning(ExitCode.LicenseSetEmpty, $"Entry with empty license set for path \"{pattern}\".")

    use ld = new LifetimeDefinition()
    let! packagedEntries = ReadEntries ld.Lifetime baseFolder config.PackagedFiles

    let unusedLockFileEntries = HashSet(lockFileEntries.Keys |> Seq.map _.Value)
    let dropEntry (sourceEntry: ISourceEntry) =
        if not <| unusedLockFileEntries.Remove sourceEntry.SourceRelativePath then
            let coveringEntry =
                unusedLockFileEntries
                |> Seq.tryFind(fun lockFileEntry ->
                    IsCoveredByPattern (LocalPath sourceEntry.SourceRelativePath) (LocalPathPattern lockFileEntry)
                )
            match coveringEntry with
            | Some entry -> unusedLockFileEntries.Remove entry |> ignore
            | None -> wp.ProduceWarning(ExitCode.FileNotCovered, $"File \"{sourceEntry.SourceRelativePath}\" is not covered by the lock file.")

    for entry in packagedEntries.SourceEntries do
        dropEntry entry

    for entry in packagedEntries.IgnoredEntries do
        dropEntry entry

    for unusedEntry in unusedLockFileEntries do
        wp.ProduceWarning(ExitCode.UnusedLockFileEntry, $"Lock file entry \"{unusedEntry}\" is not used.")
}

let private DownloadLicenses(config: Configuration, baseFolder: AbsolutePath) = task {
    let lockFile = config.LockFile |> Option.defaultWith(fun() -> failwith "lock_file not defined.")
    let licenseStorage =
        config.LicenseStorage |> Option.defaultWith(fun() -> failwith "license_storage_path not defined.")
    let! lockFileEntries = ReadLockFile(baseFolder / lockFile)
    let licenses =
        lockFileEntries.Values
        |> Seq.collect(fun entries -> entries |> Seq.collect(fun x -> Option.toArray x.SpdxExpression))
        |> Seq.distinct
        |> Seq.toArray
    use client = new HttpClient()
    for license in licenses do
        let url = $"https://spdx.org/licenses/{license}.txt"
        try
            let! content = client.GetStringAsync(url)
            let licensePath = baseFolder / licenseStorage / $"{license}.txt"
            ignore <| Directory.CreateDirectory(licensePath.Parent.Value.Value)
            do! File.WriteAllTextAsync(licensePath.Value, content)
        with
        | e ->
            eprintfn $"Error while downloading license for SPDX {license}: {e.Message}"
            raise e
    printf $"{licenses.Length} license files successfully downloaded to \"{baseFolder / licenseStorage}\"."
}

let private RunSynchronously(task: Task<'a>) =
    task.GetAwaiter().GetResult()

let private ProcessConfig(configFilePath: AbsolutePath) =
    task {
        let baseFolderPath = configFilePath.Parent.Value
        let! config = Configuration.ReadFromFile configFilePath
        return baseFolderPath, config
    }

let Process: Command -> int =
    function
    | Command.PrintVersion ->
        printfn $"DotNetLicenses v{Assembly.GetExecutingAssembly().GetName().Version}"
        0
    | Command.PrintHelp ->
        printfn """Supported arguments:
- --version - Print the program version.
- --help - Print this help message.
- [print-metadata] <config-file-path> - Print the list of the metadata provided by the configured sources.
- generate-lock <config-file-path> - Generate a license lock file and place it accordingly to the configuration.
- verify <config-file-path> - Verify the lock file against the configuration.
- download-licenses <config-file-path> - Download the licenses for the entries in the lock file.
    """
        0
    | Command.PrintMetadata configFilePath ->
        task {
            let! baseFolderPath, config = ProcessConfig configFilePath
            let wp = WarningProcessor()
            do! PrintMetadata(config, baseFolderPath, NuGetReader(), wp)
            return ProcessWarnings wp
        }
        |> RunSynchronously
    | Command.GenerateLock configFilePath ->
        task {
            let! baseFolderPath, config = ProcessConfig configFilePath
            let wp = WarningProcessor()
            do! GenerateLockFile(config, baseFolderPath, NuGetReader(), wp)
            return ProcessWarnings wp
        }
        |> RunSynchronously
    | Command.Verify config ->
        task {
            let! baseFolderPath, config = ProcessConfig config
            let wp = WarningProcessor()
            do! Verify(config, baseFolderPath, wp)
            return ProcessWarnings wp
        }
        |> RunSynchronously
    | Command.DownloadLicenses config ->
        task {
            let! baseFolderPath, config = ProcessConfig config
            do! DownloadLicenses(config, baseFolderPath)
            return int ExitCode.Success
        }
        |> RunSynchronously
