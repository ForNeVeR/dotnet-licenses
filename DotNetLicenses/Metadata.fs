// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Metadata

open System.Collections.Generic
open System.Threading.Tasks

type MetadataItem = {
    Name: string
    SPDXExpression: string
    Copyright: string
}

let ReadFrom(path: string): Task<IReadOnlyList<MetadataItem>> =
    failwithf "Not implemented yet."
