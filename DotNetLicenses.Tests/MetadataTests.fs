// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.MetadataTests

open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.Metadata
open DotNetLicenses.NuGet
open DotNetLicenses.TestFramework
open Xunit

// REUSE-IgnoreStart

[<Fact>]
let ``Get metadata from .nuspec works correctly``(): Task = task {
    let path = DataFiles.Get "Test1.nuspec"
    let! nuSpec = ReadNuSpec path
    let reference = {
        PackageId = "Package"
        Version = "1.0.0"
    }
    let metadata = GetMetadata reference nuSpec
    Assert.Equal(Some <| Package {|
        Source = reference
        Spdx = [|"MIT"|]
        Copyrights = [|"© 2024 Friedrich von Never"|]
    |}, metadata)
}

[<Fact>]
let ``Overrides work as expected``(): Task = DataFiles.Deploy "TestComplex.csproj" (fun path -> task {
    let reader = MetadataReader NuGetMock.MirroringReader
    let! metadata = reader.ReadFromProject(path, Map.ofArray [|
        { PackageId = "FVNever.Package1"; Version = "0.0.0" }, { SpdxExpressions = [|"EXPR1"|]; Copyrights = [|"C1"|] }
        { PackageId = "FVNever.Package1"; Version = "1.0.0" }, { SpdxExpressions = [|"EXPR2"|]; Copyrights = [|"C2"|] }
    |])
    Assert.Equivalent([|
        Package {|
            Source = { PackageId = "FVNever.Package1"; Version = "0.0.0" }
            Spdx = [|"EXPR1"|]
            Copyrights = [|"C1"|]
        |}
        Package {|
            Source = { PackageId = "FVNever.Package3"; Version = "0.0.0" }
            Spdx = [|"License FVNever.Package3"|]
            Copyrights = [|"Copyright FVNever.Package3"|]
        |}
    |], metadata.Items)
    Assert.Equivalent([| { PackageId = "FVNever.Package1"; Version = "0.0.0" } |], metadata.UsedOverrides)
})

// REUSE-IgnoreEnd
