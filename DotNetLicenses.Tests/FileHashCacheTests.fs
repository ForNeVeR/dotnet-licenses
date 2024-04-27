// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.FileHashCacheTests

open System.IO
open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.TestFramework
open TruePath
open Xunit

[<Fact>]
let ``FileHashCache calculates cache of a file on disk``(): Task = task {
    use cache = new FileHashCache()
    let bytes = "Hello World"B
    let path = AbsolutePath(Path.GetTempFileName())
    do! File.WriteAllBytesAsync(path.Value, bytes)
    let expectedHash = Hashes.Sha256 bytes
    let! actualHash = cache.CalculateFileHash path
    Assert.Equal(expectedHash, actualHash)
}
