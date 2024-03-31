// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.MsBuildTests

open System.IO
open System.Threading.Tasks
open DotNetLicenses.MsBuild
open DotNetLicenses.TestFramework
open Xunit

[<Fact>]
let ``MSBuild should read the project references correctly``(): Task =
    DataFiles.Deploy("Test.csproj") (fun path -> task {
        let! references = GetPackageReferences path
        Assert.Equal<PackageReference>([|{
            PackageId = "FVNever.DotNetLicenses"
            Version = "1.0.0"
        }|], references)
    })
