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
- [projectFilePath] - Process a .NET project by its path.
  Default is current directory.
    """
    | Command.PrintProjectMetadata projectFilePath ->
        let t = task {
            let! metadata = Metadata.ReadFromProject projectFilePath
            for item in metadata do
                printfn $"- {item.Name}: {item.SpdxExpression}\n  {item.Copyright}"
        }
        t.Wait()
