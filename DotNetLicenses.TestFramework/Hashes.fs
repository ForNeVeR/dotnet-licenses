// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.TestFramework.Hashes

open System
open System.Security.Cryptography

let Sha256(x: byte[]): string =
    use hash = SHA256.Create()
    hash.ComputeHash x |> Convert.ToHexString
