// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Processor

open System.IO
open System.Reflection
open System.Threading.Tasks
open DotNetLicenses.CommandLine
open DotNetLicenses.Metadata
open DotNetLicenses.NuGet

let internal PrintMetadata(
    config: Configuration,
    baseFolderPath: string,
    nuGet: INuGetReader,
    wp: WarningProcessor
): Task<int> = task {
    let reader = MetadataReader nuGet

    let overrides = config.GetOverrides wp
    let allOverrides = Set.ofSeq overrides.Keys
    let mutable usedOverrides = Set.empty

    for relativeProjectPath in config.Inputs do
        let projectPath = Path.Combine(baseFolderPath, relativeProjectPath)
        let! metadata = reader.ReadFromProject(projectPath, overrides)
        for item in metadata.Items do
            printfn $"- {item.Name}: {item.SpdxExpression}\n  {item.Copyright}"
        usedOverrides <- Set.union usedOverrides metadata.UsedOverrides

    let unusedOverrides = Set.difference allOverrides usedOverrides
    if not <| Set.isEmpty unusedOverrides then
        let stringOverrides =
            Seq.map (fun o -> $"id = \"{o.PackageId}\", version = \"{o.Version}\"") unusedOverrides
            |> String.concat ", "
        wp.ProduceWarning(ExitCode.UnusedOverride, $"Unused overrides: {stringOverrides}.")

    for message in wp.Messages do
        eprintfn $"Warning: {message}"

    let exitCode =
        if wp.Codes.Count = 0
        then ExitCode.Success
        else Seq.max wp.Codes
    return int exitCode
}

let internal GenerateLockFile(
    config: Configuration,
    baseFolderPath: string,
    nuGet: INuGetReader,
    wp: WarningProcessor
) = Task.FromResult 0

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
- generate <config-file-path> - Generate a license lock file and place it accordingly to the configuration.
    """
        0
    | Command.PrintMetadata configFilePath ->
        task {
            let! baseFolderPath, config = ProcessConfig configFilePath
            let! result = PrintMetadata(config, baseFolderPath, NuGetReader(), WarningProcessor())
            return result
        }
        |> RunSynchronously
    | Command.GenerateLock configFilePath ->
        task {
            let! baseFolderPath, config = ProcessConfig configFilePath
            let! result = GenerateLockFile(config, baseFolderPath, NuGetReader(), WarningProcessor())
            return result
        }
        |> RunSynchronously
