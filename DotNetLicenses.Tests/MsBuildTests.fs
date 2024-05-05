// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.MsBuildTests

open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.MsBuild
open DotNetLicenses.TestFramework
open TruePath
open Xunit

[<Fact>]
let ``MSBuild reads the project references correctly``(): Task =
    DataFiles.Deploy("Test.csproj") (fun path -> task {
        let! references = NuGet.ReadTransitiveProjectReferences path
        Assert.Equal<PackageReference>([| NuGetReference {
            PackageId = "FVNever.DotNetLicenses"
            Version = "1.0.0"
        }|], references)
    })

[<Fact>]
let ``MSBuild reads the project-generated artifacts correctly``(): Task =
    DataFiles.Deploy("Test.csproj") (fun project -> task {
        let! output = GetGeneratedArtifacts(project, None)
        Assert.Equal({
            FilesWithContent = [|
                project.Parent.Value / "bin" / "Release" / "net8.0" / "Test.dll"
                project.Parent.Value / "bin" / "Release" / "net8.0" / "Test.runtimeconfig.json"
                project.Parent.Value / "obj" / "DotNetToolSettings.xml"
            |]
            FilePatterns = [|
                LocalPathPattern "**/Test.deps.json"
                LocalPathPattern "**/Test.exe"
                LocalPathPattern "**/Test.pdb"
            |]
        }, output)
    })

[<Fact>]
let ``MSBuild deduplicates the project references across solution``(): Task =
    DataFiles.DeployGroup [|"Test.csproj"; "Test2.csproj"; "Test.sln"|] (fun path -> task {
        let solution = path / "Test.sln"
        let! references = NuGet.ReadTransitiveProjectReferences solution
        Assert.Equal<PackageReference>([| NuGetReference {
            PackageId = "FVNever.DotNetLicenses"
            Version = "1.0.0"
        }|], references)
    })

[<Fact>]
let ``MSBuild reads the solution-generated artifacts correctly``(): Task =
    DataFiles.DeployGroup [|"Test.csproj"; "Test2.csproj"; "Test.sln"|] (fun path -> task {
        let solution = path / "Test.sln"
        let! output = GetGeneratedArtifacts(solution, None)
        Assert.Equal({
            FilesWithContent = [|
                path / "bin" / "Release" / "net8.0" / "Test.dll"
                path / "bin" / "Release" / "net8.0" / "Test.runtimeconfig.json"
                path / "bin" / "Release" / "net8.0" / "Test2.dll"
                path / "bin" / "Release" / "net8.0" / "Test2.runtimeconfig.json"
                path / "obj" / "DotNetToolSettings.xml"
            |]
            FilePatterns = [|
                LocalPathPattern "**/Test.deps.json"
                LocalPathPattern "**/Test.exe"
                LocalPathPattern "**/Test.pdb"
                LocalPathPattern "**/Test2.deps.json"
                LocalPathPattern "**/Test2.exe"
                LocalPathPattern "**/Test2.pdb"
            |]
        }, output)
    })
