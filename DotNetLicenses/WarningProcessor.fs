// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses

open System.Collections.Generic
open DotNetLicenses.CommandLine

type WarningProcessor() =
    let codes = HashSet()
    let messages = ResizeArray()

    member val Codes: IReadOnlySet<ExitCode> = codes
    member val Messages: IReadOnlyList<string> = messages

    member _.ProduceWarning(code: ExitCode, message: string): unit =
        codes.Add code |> ignore
        messages.Add message
