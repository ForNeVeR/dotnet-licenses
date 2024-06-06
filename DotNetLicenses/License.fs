// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses

open System.Collections.Generic
open System.Collections.Immutable
open System.IO
open System.Net.Http
open System.Threading.Tasks


[<Interface>]
type ILicense =
    abstract member SpdxExpression: string
    abstract member CopyrightNotices: ImmutableArray<string>
    abstract member GetText: unit -> Task<string>

    static member Merge(licenses: IReadOnlyList<ILicense>): ILicense option =
        if licenses.Count = 0 then None
        else Some (
            let spdx = licenses |> Seq.map _.SpdxExpression |> Seq.distinct |> String.concat " AND "
            let copyrightNotices = licenses |> Seq.collect _.CopyrightNotices |> Seq.distinct |> Seq.toArray
            { new ILicense with
                member _.SpdxExpression = spdx
                member _.CopyrightNotices = ImmutableArray.ToImmutableArray copyrightNotices
                member _.GetText() = task {
                    let! contents = licenses |> Seq.map _.GetText() |> Task.WhenAll
                    let distinctContents = contents |> Seq.distinct |> Seq.toArray
                    if distinctContents.Length > 1 then
                        failwithf $"Multiple licenses with different content: ${licenses}"
                        // TODO: Support several licenses this way
                    return Array.exactlyOne distinctContents
                }
            }
        )

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
