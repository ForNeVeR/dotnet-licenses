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
- [path] - Process a .NET project residing directly in the specified path.
  Default is current directory.
    """
    | Command.PrintMetadata path ->
        let t = task {
            let! metadata = Metadata.ReadFrom path
            for item in metadata do
                printfn $"- {item.Name}: {item.SPDXExpression}\n  {item.Copyright}"
        }
        t.Wait()
