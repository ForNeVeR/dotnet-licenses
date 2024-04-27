// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses.TestFramework

open System
open System.IO
open TruePath

type DisposableDirectory =
    { Path: AbsolutePath }

    interface IDisposable with
        member this.Dispose() =
            Directory.Delete(this.Path.Value, true)

    static member Create() =
        let path = AbsolutePath(Path.GetTempFileName())
        File.Delete path.Value
        Directory.CreateDirectory path.Value |> ignore
        { Path = path }

    member this.MakeSubDirs(paths: LocalPath seq) =
        paths
        |> Seq.map (fun p -> this.Path / p)
        |> Seq.iter(fun d -> Directory.CreateDirectory d.Value |> ignore)
