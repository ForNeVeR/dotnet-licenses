// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses

open System
open System.Collections.Concurrent
open System.IO
open System.Security.Cryptography
open System.Threading
open System.Threading.Tasks

type FileHashCache() =
    let cts = new CancellationTokenSource()
    let map = ConcurrentDictionary<string, Task<string>>()

    member _.CalculateFileHash(path: string): Task<string> =
        map.GetOrAdd(path, fun path -> task {
            let! bytes = File.ReadAllBytesAsync(path, cts.Token)
            use sha = SHA256.Create()
            let hash = sha.ComputeHash bytes
            return Convert.ToHexString hash
        })

    interface IDisposable with
        member _.Dispose() = cts.Dispose()
