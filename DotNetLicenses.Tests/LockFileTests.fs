// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.LockFileTests

open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open DotNetLicenses.LockFile
open TruePath
open Xunit

[<Fact>]
let ``LockFile should be have expected format``(): Task =
    let item id = {
        SourceId = id
        SourceVersion = "1.0.0"
        Spdx = "MIT"
        Copyright = "none"
    }
    let data = Dictionary<_, IReadOnlyList<_>>()
    data.Add("x.txt", [| item "a"; item "b" |])
    data.Add("y.txt", [| item "a" |])

    let expectedContent = """[["x.txt"]]
source_id = "a"
source_version = "1.0.0"
spdx = "MIT"
copyright = "none"
[["x.txt"]]
source_id = "b"
source_version = "1.0.0"
spdx = "MIT"
copyright = "none"
[["y.txt"]]
source_id = "a"
source_version = "1.0.0"
spdx = "MIT"
copyright = "none"
"""
    task {
        let lockFilePath = AbsolutePath <| Path.GetTempFileName()
        do! SaveLockFile(lockFilePath, data)
        let! content = File.ReadAllTextAsync lockFilePath.Value
        Assert.Equal(expectedContent.ReplaceLineEndings "\n", content.ReplaceLineEndings "\n")
    }
