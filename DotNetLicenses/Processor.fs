// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Processor

open System.IO
open System.Reflection
open System.Threading.Tasks
open DotNetLicenses.CommandLine

let private RunSynchronously(task: Task) =
    task.Wait()

let Process: Command -> unit =
    function
    | Command.PrintVersion ->
        printfn $"DotNetLicenses v{Assembly.GetExecutingAssembly().GetName().Version}"
    | Command.PrintHelp ->
        printfn """Supported arguments:
- --version - Print the program version.
- --help - Print this help message.
- <config-file-path> - Print the licenses used by the projects designated by the config file.
    """
    | Command.PrintMetadata configFilePath ->
        task {
            let baseFolderPath = Path.GetDirectoryName configFilePath
            let! config = Configuration.ReadFromFile configFilePath
            for relativeProjectPath in config.Inputs do
                let projectPath = Path.Combine(baseFolderPath, relativeProjectPath)
                let! metadata = Metadata.ReadFromProject(projectPath, Map.empty) // TODO: Read overrides from config
                for item in metadata do
                    printfn $"- {item.Name}: {item.SpdxExpression}\n  {item.Copyright}"
        }
        |> RunSynchronously
