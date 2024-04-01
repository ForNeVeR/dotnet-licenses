// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Processor

open System.Reflection
open DotNetLicenses.CommandLine

[<NoCompilerInlining>]
let Perform: Command -> unit =
    function
    | Command.PrintVersion ->
        printfn $"DotNetLicenses v{Assembly.GetExecutingAssembly().GetName().Version}"
    | Command.PrintHelp ->
        printfn """Supported arguments:
- --version - Print the program version.
- --help - Print this help message.
- <config-file-path> - Print the licenses used by the projects from the config file.
    """
    | Command.PrintMetadata projectFilePath ->
        let t = task {
            let! metadata = Metadata.ReadFromProject projectFilePath
            for item in metadata do
                printfn $"- {item.Name}: {item.SpdxExpression}\n  {item.Copyright}"
        }
        t.Wait()
