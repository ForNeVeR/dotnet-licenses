// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses

open System.IO
open System.Threading.Tasks
open Tomlyn

[<CLIMutable>]
type Configuration =
    {
        Inputs: string[]
    }

    static member Read(stream: Stream, filePath: string option): Task<Configuration> = task {
        use reader = new StreamReader(stream)
        let! text = reader.ReadToEndAsync()
        return Toml.ToModel<Configuration>(text, Option.toObj filePath, options = null)
    }

    static member ReadFromFile(path: string): Task<Configuration> = task {
        use stream = new FileStream(path, FileMode.Open, FileAccess.Read)
        return! Configuration.Read(stream, Some path)
    }
