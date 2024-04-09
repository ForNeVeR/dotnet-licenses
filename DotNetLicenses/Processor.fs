// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Processor

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Threading.Tasks
open DotNetLicenses.CommandLine
open DotNetLicenses.LockFile
open DotNetLicenses.Metadata
open DotNetLicenses.NuGet
open JetBrains.Lifetimes

let private WarnUnusedOverrides allOverrides usedOverrides (wp: WarningProcessor) =
    let allOverrides = Set.ofSeq allOverrides
    let unusedOverrides = Set.difference allOverrides usedOverrides

    if not <| Set.isEmpty unusedOverrides then
        let stringOverrides =
            Seq.map (fun o -> $"id = \"{o.PackageId}\", version = \"{o.Version}\"") unusedOverrides
            |> String.concat ", "
        wp.ProduceWarning(ExitCode.UnusedOverride, $"Unused overrides: {stringOverrides}.")

let private ProcessWarnings(wp: WarningProcessor) =
    for message in wp.Messages do
        eprintfn $"Warning: {message}"

    let exitCode =
        if wp.Codes.Count = 0
        then ExitCode.Success
        else Seq.max wp.Codes
    int exitCode

let private CollectMetadata (config: Configuration) baseFolderPath nuGet wp = task {
    let reader = MetadataReader nuGet

    let overrides = config.GetOverrides wp
    let mutable usedOverrides = Set.empty

    let metadataList = ResizeArray()
    for relativeProjectPath in config.Inputs do
        let projectPath = Path.Combine(baseFolderPath, relativeProjectPath)
        let! metadata = reader.ReadFromProject(projectPath, overrides)
        usedOverrides <- Set.union usedOverrides metadata.UsedOverrides
        metadataList.AddRange metadata.Items

    WarnUnusedOverrides overrides.Keys usedOverrides wp
    return metadataList
}


let internal PrintMetadata(
    config: Configuration,
    baseFolderPath: string,
    nuGet: INuGetReader,
    wp: WarningProcessor
): Task = task {
    let! metadata = CollectMetadata config baseFolderPath nuGet wp
    for item in metadata do
        printfn $"- {item.Id}: {item.Spdx}\n  {item.Copyright}"
}

let internal GenerateLockFile(
    config: Configuration,
    baseFolderPath: string,
    nuGet: INuGetReader,
    wp: WarningProcessor
): Task = task {
    if String.IsNullOrWhiteSpace config.LockFile
    then wp.ProduceWarning(ExitCode.LockFileIsNotDefined, "lock_file is not specified in the configuration.")
    else

    if config.Package = null
    then wp.ProduceWarning(ExitCode.PackageIsNotDefined, "package is not specified in the configuration.")
    else

    let lockFilePath = Path.Combine(baseFolderPath, config.LockFile)
    let! metadata = CollectMetadata config baseFolderPath nuGet wp
    let packages = metadata |> Seq.map _.Source |> Seq.toArray
    let packageMetadata = metadata |> Seq.map (fun m -> m.Source, m) |> Map.ofSeq
    use ld = new LifetimeDefinition()

    let! sourceEntries = Sources.ReadEntries ld.Lifetime baseFolderPath config.Package

    let lockFileContent = Dictionary<_, IReadOnlyList<LockFileItem>>()
    use cache = new FileHashCache()
    for entry in sourceEntries do
        let! packageEntries = nuGet.FindFile cache packages entry
        match packageEntries.Length with
        | 0 -> wp.ProduceWarning(ExitCode.LicenseForFileNotFound, $"No license found for file \"{entry.SourceRelativePath}\".")
        | 1 ->
            let package = Seq.exactlyOne packageEntries
            let metadata = packageMetadata[package]
            lockFileContent.Add(entry.SourceRelativePath, [|{
                SourceId = package.PackageId
                SourceVersion = package.Version
                Spdx = metadata.Spdx
                Copyright = metadata.Copyright
            }|])
        | _ ->
            failwithf $"Several package sources found for one file: \"{entry.SourceRelativePath}\"."
            // TODO[#28]: If all the packages yield the same license, just let it be.
            // TODO[#28]: Otherwise, report this situation as a warning, and collect all the licenses.

    do! SaveLockFile(lockFilePath, lockFileContent)
}

let private RunSynchronously(task: Task<'a>) =
    task.GetAwaiter().GetResult()

let private ProcessConfig(configFilePath: string) =
    task {
        let baseFolderPath = Path.GetDirectoryName configFilePath
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
