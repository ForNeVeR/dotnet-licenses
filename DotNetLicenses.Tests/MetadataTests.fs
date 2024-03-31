// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.MetadataTests

open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.Metadata
open DotNetLicenses.TestFramework
open Xunit

[<Fact>]
let ``Get metadata from .nuspec works correctly``(): Task = task {
    let path = DataFiles.Get "Test1.nuspec"
    let! nuSpec = NuGet.ReadNuSpec path
    let metadata = GetMetadata nuSpec
    Assert.Equal({
        Name = "FVNever.DotNetLicenses"
        SpdxExpression = "MIT"
        Copyright = "© 2024 Friedrich von Never"
    }, metadata)
}
