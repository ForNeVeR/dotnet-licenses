// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Reuse

open DotNetLicenses.Sources

type ReuseLicenseEntry = {
    SpdxExpressions: string[]
    CopyrightStatements: string[]
}

let CollectLicenses (source: ReuseSource) (entry: ISourceEntry): ReuseLicenseEntry option =
    failwithf "TODO"
