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

let internal ProcessPrintMetadata(
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

    return int <| Seq.max wp.Codes
}

let private RunSynchronously(task: Task<'a>) =
    task.GetAwaiter().GetResult()

let Process: Command -> int =
    function
    | Command.PrintVersion ->
        printfn $"DotNetLicenses v{Assembly.GetExecutingAssembly().GetName().Version}"
        0
    | Command.PrintHelp ->
        printfn """Supported arguments:
- --version - Print the program version.
- --help - Print this help message.
- <config-file-path> - Print the licenses used by the projects designated by the config file.
    """
        0
    | Command.PrintMetadata configFilePath ->
        task {
            let baseFolderPath = Path.GetDirectoryName configFilePath
            let! config = Configuration.ReadFromFile configFilePath
            let nuGet = NuGetReader()

            let! result = ProcessPrintMetadata(config, baseFolderPath, nuGet, WarningProcessor())
            return result
        }
        |> RunSynchronously
