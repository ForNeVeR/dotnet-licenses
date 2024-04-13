// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses.TestFramework

open System
open System.IO
open TruePath

type DisposableDirectory =
    { Path: StrictAbsolutePath }

    interface IDisposable with
        member this.Dispose() =
            Directory.Delete(this.Path.Value, true)

    static member Create() =
        let path = StrictAbsolutePath <| Path.GetTempFileName()
        File.Delete path.Value
        Directory.CreateDirectory path.Value |> ignore
        { Path = path }
