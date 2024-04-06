// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses.TestFramework

open System
open System.IO

type DisposableDirectory =
    { Path: string }

    interface IDisposable with
        member this.Dispose() =
            Directory.Delete(this.Path, true)

    static member Create() =
        let path = Path.GetTempFileName()
        File.Delete(path)
        Directory.CreateDirectory(path) |> ignore
        { Path = path }
