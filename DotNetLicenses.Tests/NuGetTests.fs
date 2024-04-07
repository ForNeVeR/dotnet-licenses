// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.NuGetTests

open System.IO
open System.Threading.Tasks
open DotNetLicenses.NuGet
open DotNetLicenses.TestFramework
open Xunit

[<Fact>]
let ``NuSpec file path should be determined correctly``(): unit =
    let nuGetPackagesRootPath = PackagesFolderPath
    let package = "FVNever.DotNetLicenses"
    let version = "1.0.0"
    let expectedPath = Path.Combine(
        nuGetPackagesRootPath,
        package.ToLowerInvariant(),
        version.ToLowerInvariant(),
        package.ToLowerInvariant() + ".nuspec"
    )
    Assert.Equal(expectedPath, GetNuSpecFilePath {
        PackageId = package
        Version = version
    })

[<Theory>]
[<InlineData("Test1.nuspec"); InlineData("Test2.nuspec")>]
let ``NuSpec file should be read correctly``(fileName: string): Task = task {
    let path = DataFiles.Get fileName
    let! nuSpec = ReadNuSpec path
    Assert.Equal({
        Metadata = {
            Id = "FVNever.DotNetLicenses"
            Version = "0.0.0"
            License = {
                Type = "expression"
                Value = "MIT"
            }
            Copyright = "© 2024 Friedrich von Never"
        }
    }, nuSpec)
}

[<Fact>]
let ``Package file searcher works correctly``(): unit =
    Assert.Fail()
