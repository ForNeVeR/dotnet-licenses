// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Processor

open System.Collections.Generic
open System.IO
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

let private GetMetadata (projectMetadataReader: MetadataReader) (baseFolderPath: AbsolutePath) overrides = function
    | MetadataSource.NuGet source ->
        let projectPath = baseFolderPath / source.Include
        projectMetadataReader.ReadFromProject(projectPath, overrides)
    | MetadataSource.License source ->
        Task.FromResult {
            Items = [| License source |].AsReadOnly()
            UsedOverrides = Set.empty
        }
    | MetadataSource.Reuse source ->
        Task.FromResult {
            Items = [| Reuse source |].AsReadOnly()
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

let internal PrintPackages(
    config: Configuration,
    baseFolderPath: AbsolutePath,
    nuGet: INuGetReader,
    wp: WarningProcessor
): Task = task {
    let! metadata = CollectMetadata config baseFolderPath nuGet wp
    for item in metadata do
        match item with
        | Package package ->
            printfn $"- {package.Source.PackageId} {package.Source.Version}: {package.Spdx}\n  {package.Copyright}"
        | _ -> ()
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

    let! sourceEntries = ReadEntries ld.Lifetime baseFolderPath packagedFiles

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

    let findLicensesForFile entry: Task<LockFileEntry[]> = task {
        let! packageEntries = nuGet.FindFile hashCache packages entry
        let packageSearchResults =
            packageEntries
            |> Seq.map (fun package ->
                let metadata = packageMetadata[package]
                match metadata with
                | Package p ->
                    LocalPathPattern entry.SourceRelativePath, {
                        SourceId = package.PackageId
                        SourceVersion = package.Version
                        Spdx = [|p.Spdx|]
                        Copyright = [|p.Copyright|]
                    }
                | _ -> failwithf $"Unexpected metadata type in object {metadata}."
            )
        let licenseSearchResults =
            findExplicitLicenses entry
            |> Seq.map (fun license -> LocalPathPattern entry.SourceRelativePath, {
                SourceId = null
                SourceVersion = null
                Spdx = [|license.Spdx|]
                Copyright = [|license.Copyright|]
            })
        let! reuseEntries = findReuseLicenses entry
        let reuseSearchResults =
            reuseEntries
            |> Seq.map (fun license -> LocalPathPattern entry.SourceRelativePath, {
                SourceId = null
                SourceVersion = null
                Spdx = Seq.toArray license.SpdxExpressions
                Copyright = Seq.toArray license.CopyrightStatements
            })
        let reuseCombinedSearchResults =
            findReuseCombinedLicenses entry
            |> Seq.map(fun fe -> LocalPathPattern entry.SourceRelativePath, {
                SourceId = null
                SourceVersion = null
                Spdx = Seq.toArray fe.LicenseIdentifiers
                Copyright = Seq.toArray fe.CopyrightStatements
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
    for entry in sourceEntries do
        let! resultEntries = findLicensesForFile entry
        match resultEntries.Length with
        | 0 -> wp.ProduceWarning(ExitCode.LicenseForFileNotFound, $"No license found for file \"{entry.SourceRelativePath}\".")
        | 1 -> lockFileContent.AddRange(resultEntries)
        | _ ->
            failwithf $"Several licenses found for one file: \"{entry.SourceRelativePath}\"."
            // TODO[#28]: If all the packages yield the same license, just let it be.
            // TODO[#28]: Otherwise, report this situation as a warning, and collect all the licenses.

    do! SaveLockFile(lockFilePath, lockFileContent)
}

let private RunSynchronously(task: Task<'a>) =
    task.GetAwaiter().GetResult()

let private ProcessConfig(configFilePath: string) =
    task {
        let baseFolderPath = AbsolutePath(Path.GetFullPath(Path.GetDirectoryName configFilePath))
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
- [print-packages] <config-file-path> - Print the licenses used by the projects designated by the configuration.
- generate-lock <config-file-path> - Generate a license lock file and place it accordingly to the configuration.
    """
        0
    | Command.PrintPackages configFilePath ->
        task {
            let! baseFolderPath, config = ProcessConfig configFilePath
            let wp = WarningProcessor()
            do! PrintPackages(config, baseFolderPath, NuGetReader(), wp)
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
