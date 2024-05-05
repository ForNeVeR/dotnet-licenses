// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses

open System.Collections.Immutable
open System.IO
open System.Net.Http
open System.Threading.Tasks

type ILicense =
    abstract member SpdxExpression: string
    abstract member CopyrightNotices: ImmutableArray<string>
    abstract member GetText: unit -> Task<string>

type SpdxLicense =
    {
        SpdxExpression: string
        CopyrightNotices: ImmutableArray<string>
    }
    interface ILicense with
        member this.SpdxExpression = this.SpdxExpression
        member this.CopyrightNotices = this.CopyrightNotices
        member this.GetText() = task {
            use client = new HttpClient()
            let url = $"https://spdx.org/licenses/{this.SpdxExpression}.txt"
            return! client.GetStringAsync(url)
        }

type DotNetSdkLicense =
    | DotNetSdkLicense

    interface ILicense with
        member _.SpdxExpression = "LicenseRef-DotNetSdk"
        member _.CopyrightNotices = ImmutableArray.Create()
        member this.GetText() = task {
            let! sdk = DotNetSdk.Location
            return! File.ReadAllTextAsync((sdk / "LICENSE.txt").Value)
        }
