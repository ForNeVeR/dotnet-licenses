// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.MetadataTests

open System.Collections.Immutable
open System.IO
open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.Metadata
open DotNetLicenses.NuGet
open DotNetLicenses.TestFramework
open Xunit

// REUSE-IgnoreStart

[<Fact>]
let ``Get metadata from .nuspec works correctly``(): Task = task {
    use dir = DisposableDirectory.Create()
    let sourceRoots = [| dir.Path |]
    let coords = {
        PackageId = "Package"
        Version = "1.0.0"
    }
    let nuSpec = DataFiles.Get "Test1.nuspec"
    let! targetPath = GetNuSpecFilePath sourceRoots coords RootResolveBehavior.Any
    let targetPath = Option.get targetPath
    Directory.CreateDirectory(targetPath.Parent.Value.Value) |> ignore
    File.Copy(nuSpec.Value, targetPath.Value)

    let projectFile = dir.Path / "imaginative-file.csproj"
    let reference = NuGetReference(projectFile, coords)
    let nuGet = NuGetMock.DirectNuSpecReader(dir.Path)
    let! metadata = GetMetadata projectFile nuGet reference
    Assert.Equal(Some <| Package {|
        Source = reference
        License = {
            SpdxLicense.SpdxExpression = "MIT"
            CopyrightNotices = ImmutableArray.Create "© 2024 Friedrich von Never"
        }
    |}, metadata)
}

[<Fact>]
let ``Overrides work as expected``(): Task = DataFiles.Deploy "TestComplex.csproj" (fun path -> task {
    let reader = MetadataReader NuGetMock.MirroringReader
    let! metadata = reader.ReadFromProject(path, Map.ofArray [|
        { PackageId = "FVNever.Package1"; Version = "0.0.0" }, { SpdxExpression = "EXPR1"; CopyrightNotices = [|"C1"|] }
        { PackageId = "FVNever.Package1"; Version = "1.0.0" }, { SpdxExpression = "EXPR2"; CopyrightNotices = [|"C2"|] }
    |])
    Assert.Equivalent([|
        Package {|
            Source = NuGetReference(path, { PackageId = "FVNever.Package1"; Version = "0.0.0" })
            License = {
                SpdxLicense.SpdxExpression = "EXPR1"
                CopyrightNotices = ImmutableArray.Create "C1"
            }
        |}
        Package {|
            Source = NuGetReference(path, { PackageId = "FVNever.Package3"; Version = "0.0.0" })
            License = {
                SpdxLicense.SpdxExpression = "License FVNever.Package3"
                CopyrightNotices = ImmutableArray.Create "Copyright FVNever.Package3"
            }
        |}
    |], metadata.Items)
    Assert.Equivalent([| { PackageId = "FVNever.Package1"; Version = "0.0.0" } |], metadata.UsedOverrides)
})

// REUSE-IgnoreEnd
