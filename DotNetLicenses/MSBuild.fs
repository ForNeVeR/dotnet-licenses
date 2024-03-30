// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.MSBuild

open System.Collections.Generic
open System.Threading.Tasks

type PackageReference = {
    PackageId: string
    Version: string
}

let GetPackageReferences(projectFilePath: string): Task<IList<PackageReference>> =
    failwithf "TODO"
