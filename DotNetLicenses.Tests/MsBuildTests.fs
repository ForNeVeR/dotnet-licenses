// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.MsBuildTests

open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.MsBuild
open DotNetLicenses.TestFramework
open Xunit

[<Fact>]
let ``MSBuild reads the project references correctly``(): Task =
    DataFiles.Deploy("Test.csproj") (fun path -> task {
        let! references = GetPackageReferences path
        Assert.Equal<PackageReference>([|{
            PackageId = "FVNever.DotNetLicenses"
            Version = "1.0.0"
        }|], references)
    })

[<Fact>]
let ``MSBuild reads the project-generated artifacts correctly``(): Task =
    DataFiles.Deploy("Test.csproj") (fun project -> task {
        let! output = GetProjectGeneratedArtifacts project
        Assert.Equal(project.Parent.Value / "bin" / "Release" / "net8.0" / "Test.dll", output)
    })
