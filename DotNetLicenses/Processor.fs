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
            let nuGet = NuGetReader()
            let reader = MetadataReader nuGet

            let baseFolderPath = Path.GetDirectoryName configFilePath
            let! config = Configuration.ReadFromFile configFilePath
            let wp = WarningProcessor()

            for relativeProjectPath in config.Inputs do
                let projectPath = Path.Combine(baseFolderPath, relativeProjectPath)
                let! metadata = reader.ReadFromProject(projectPath, config.GetOverrides wp)
                for item in metadata do
                    printfn $"- {item.Name}: {item.SpdxExpression}\n  {item.Copyright}"

            for message in wp.Messages do
                eprintfn $"Warning: {message}"

            return int <| Seq.max wp.Codes
        }
        |> RunSynchronously
