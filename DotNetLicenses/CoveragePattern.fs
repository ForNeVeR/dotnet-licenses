// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.CoveragePattern

open System.Threading.Tasks
open DotNetLicenses.LockFile
open DotNetLicenses.Metadata

let CollectCoveredFileLicense(metadataItems: MetadataItem seq) (sourceEntry: ISourceEntry): Task<LockFileItem seq> = task {
    return failwith "TODO"
}
