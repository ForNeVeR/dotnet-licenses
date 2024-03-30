// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Metadata

open System.Collections.Generic
open System.Collections.ObjectModel
open System.Threading.Tasks
open DotNetLicenses.NuGet

type MetadataItem = {
    Name: string
    SpdxExpression: string
    Copyright: string
}

let internal GetMetadata(nuSpec: NuSpec): MetadataItem =
    let metadata = nuSpec.Metadata
    let license = metadata.License
    if license.Type <> "expression" then
        failwithf $"Unsupported license type: {license.Type}"
    {
        Name = metadata.Id
        SpdxExpression = metadata.License.Value
        Copyright = metadata.Copyright
    }

let ReadFromProject(projectFilePath: string): Task<ReadOnlyCollection<MetadataItem>> = task {
    let! packageReferences = MSBuild.GetPackageReferences projectFilePath
    let result = ResizeArray(packageReferences.Count)
    for reference in packageReferences do
        let nuSpecPath = GetNuSpecFilePath reference
        let! nuSpec = ReadNuSpec nuSpecPath
        let metadata = GetMetadata nuSpec
        result.Add metadata
    return result.AsReadOnly()
}
