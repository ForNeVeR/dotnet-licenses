// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.MetadataTests

open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.Metadata
open DotNetLicenses.NuGet
open DotNetLicenses.TestFramework
open TruePath
open Xunit

[<Fact>]
let ``Get metadata from .nuspec works correctly``(): Task = task {
    let path = DataFiles.Get "Test1.nuspec"
    let! nuSpec = ReadNuSpec <| AbsolutePath.op_Implicit path
    let reference = {
        PackageId = "Package"
        Version = "1.0.0"
    }
    let metadata = GetMetadata reference nuSpec
    Assert.Equal(Package {|
        Source = reference
        Spdx = "MIT"
        Copyright = "© 2024 Friedrich von Never"
    |}, metadata)
}

[<Fact>]
let ``Overrides work as expected``(): Task = task {
    let path = DataFiles.Get "TestComplex.csproj"

    let reader = MetadataReader NuGetMock.MirroringReader
    let! metadata = reader.ReadFromProject(path, Map.ofArray [|
        { PackageId = "FVNever.Package1"; Version = "0.0.0" }, { SpdxExpression = "EXPR1"; Copyright = "C1" }
        { PackageId = "FVNever.Package1"; Version = "1.0.0" }, { SpdxExpression = "EXPR2"; Copyright = "C2" }
    |])
    Assert.Equivalent([|
        Package {|
            Source = { PackageId = "FVNever.Package1"; Version = "0.0.0" }
            Spdx = "EXPR1"
            Copyright = "C1"
        |}
        Package {|
            Source = { PackageId = "FVNever.Package3"; Version = "0.0.0" }
            Spdx = "License FVNever.Package3"
            Copyright = "Copyright FVNever.Package3"
        |}
    |], metadata.Items)
    Assert.Equivalent([| { PackageId = "FVNever.Package1"; Version = "0.0.0" } |], metadata.UsedOverrides)
}
