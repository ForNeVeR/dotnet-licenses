// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses

open System.IO
open System.Threading.Tasks
open TruePath

type PackageReference = {
    PackageId: string
    Version: string
}

type MetadataOverride = {
    SpdxExpression: string
    Copyright: string
}

type IgnorePattern =
    | Preset of name: string

type PackageSpec =
    {
        Source: PackageSource
        Ignores: IgnorePattern[]
    }

and PackageSource =
    | Directory of path: LocalPath
    | Zip of path: LocalPathPattern

type ISourceEntry =
    abstract member Source: PackageSpec
    abstract member SourceRelativePath: string
    abstract member ReadContent: unit -> Task<Stream>
    abstract member CalculateHash: unit -> Task<string>
