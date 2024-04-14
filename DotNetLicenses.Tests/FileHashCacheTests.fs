// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.FileHashCacheTests

open System
open System.IO
open System.Security.Cryptography
open System.Threading.Tasks
open DotNetLicenses
open TruePath
open Xunit

let private CalculateSha256(bytes: byte[]) =
    use sha256 = SHA256.Create()
    sha256.ComputeHash(bytes) |> Convert.ToHexString

[<Fact>]
let ``FileHashCache calculates cache of a file on disk``(): Task = task {
    use cache = new FileHashCache()
    let bytes = "Hello World"B
    let path = AbsolutePath <| Path.GetTempFileName()
    do! File.WriteAllBytesAsync(path.Value, bytes)
    let expectedHash = CalculateSha256 bytes
    let! actualHash = cache.CalculateFileHash path
    Assert.Equal(expectedHash, actualHash)
}
