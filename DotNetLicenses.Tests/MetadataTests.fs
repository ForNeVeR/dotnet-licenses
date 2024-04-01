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

[<Fact>]
let ``Overrides work as expected``(): Task = task {
    // TODO: Override the NuGet reader to read a file from the test data
    let path = DataFiles.Get "TestComplex.csproj"
    let! metadata = ReadFromProject(path, Map.ofArray [|
        { PackageId = "FVNever.Package1"; Version = "0.0.0" }, { SpdxExpression = "EXPR1"; Copyright = "C1" }
        { PackageId = "FVNever.Package1"; Version = "0.0.0" }, { SpdxExpression = "EXPR2"; Copyright = "C2" }
    |])
    Assert.Equal<MetadataItem>(Set.ofArray [|
        {
            Name = "FVNever.Package1"
            SpdxExpression = "EXPR1"
            Copyright = "C1"
        }
        {
            Name = "FVNever.Package3"
            SpdxExpression = "MIT"
            Copyright = "Copyright 3"
        }
    |], Set.ofSeq metadata)
}
