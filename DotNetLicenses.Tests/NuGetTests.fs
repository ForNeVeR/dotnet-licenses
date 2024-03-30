// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.NuGetTEsts

open System.IO
open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.NuGet
open DotNetLicenses.TestFramework
open Xunit

[<Fact>]
let ``NuSpec file path should be determined correctly``(): unit =
    let nuGetPackagesRootPath = NuGet.PackagesFolderPath
    let package = "FVNever.DotNetLicenses"
    let version = "1.0.0"
    Assert.Equal(Path.Combine(nuGetPackagesRootPath, package, version), NuGet.GetNuSpecFilePath {
        PackageId = package
        Version = version
    })

[<Fact>]
let ``NuSpec file should be read correctly``(): Task = task {
    let path = DataFiles.Get "Test.nuspec"
    let! nuSpec = NuGet.ReadNuSpec path
    Assert.Equal({
        Metadata = {
            Id = "FVNever.DotNetLicenses"
            License = {
                Type = "expression"
                Value = "MIT"
            }
            Copyright = "© 2024 Friedrich von Never;\n© Microsoft Corporation. All rights reserved."
        }
    }, nuSpec)
}
