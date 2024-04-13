// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Processor

open System.Collections.Generic
open System.IO
open System.Reflection
open System.Threading.Tasks
open DotNetLicenses.CommandLine
open DotNetLicenses.LockFile
open DotNetLicenses.Metadata
open DotNetLicenses.NuGet
open DotNetLicenses.Sources
open JetBrains.Lifetimes
open Microsoft.Extensions.FileSystemGlobbing
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
            Items = [| {
                Source = License source
                Spdx = source.Spdx
                Copyright = source.Copyright
            } |].AsReadOnly()
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
        let sourceInfo =
            match item.Source with
            | Package package -> $"{package.PackageId} {package.Version}"
            | License _ -> "License"
        printfn $"- {sourceInfo}: {item.Spdx}\n  {item.Copyright}"
}

let private IsCoveredByPattern (path: RelativePath) (pattern: LocalPathPattern) =
    let matcher = Matcher().AddInclude pattern.Value
    let result = matcher.Match path.Value
    result.HasMatches

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
        |> Seq.map _.Source
        |> Seq.collect(function
            | Package p -> [| p |]
            | _ -> Array.empty
        )
        |> Seq.toArray
    let licenses =
        metadata
        |> Seq.map _.Source
        |> Seq.collect(function
            | License l -> [| l |]
            | _ -> Array.empty
        )
        |> Seq.toArray
    let packageMetadata =
        metadata
        |> Seq.collect(fun m ->
            match m.Source with
            | Package p -> [| p, m |]
            | _ -> Array.empty
        )
        |> Map.ofSeq
    use ld = new LifetimeDefinition()

    let! sourceEntries = ReadEntries ld.Lifetime baseFolderPath packagedFiles

    let findExplicitLicenses(entry: ISourceEntry) =
        licenses
        |> Seq.filter(fun license ->
            license.FilesCovered
            |> Seq.exists(fun pattern -> IsCoveredByPattern (RelativePath entry.SourceRelativePath) pattern)
        )

    use cache = new FileHashCache()
    let findLicensesForFile entry = task {
        let! packageEntries = nuGet.FindFile cache packages entry
        let packageSearchResults =
            packageEntries
            |> Seq.map (fun package ->
                let metadata = packageMetadata[package]
                {
                    SourceId = package.PackageId
                    SourceVersion = package.Version
                    Spdx = metadata.Spdx
                    Copyright = metadata.Copyright
                }
            )
        let licenseSearchResults =
            findExplicitLicenses entry
            |> Seq.map (fun license -> {
                SourceId = null
                SourceVersion = null
                Spdx = license.Spdx
                Copyright = license.Copyright
            })
        return Seq.concat [|packageSearchResults; licenseSearchResults|] |> Seq.toArray
    }

    let lockFileContent = Dictionary<_, IReadOnlyList<LockFileItem>>()
    for entry in sourceEntries do
        let! resultEntries = findLicensesForFile entry
        match resultEntries.Length with
        | 0 -> wp.ProduceWarning(ExitCode.LicenseForFileNotFound, $"No license found for file \"{entry.SourceRelativePath}\".")
        | 1 -> lockFileContent.Add(entry.SourceRelativePath, resultEntries)
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
        let baseFolderPath = AbsolutePath <| Path.GetDirectoryName configFilePath
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
- [print] <config-file-path> - Print the licenses used by the projects designated by the configuration.
- generate-lock <config-file-path> - Generate a license lock file and place it accordingly to the configuration.
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
