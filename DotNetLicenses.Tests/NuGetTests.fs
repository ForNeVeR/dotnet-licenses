// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.NuGetTests

open System
open System.IO
open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.NuGet
open DotNetLicenses.Sources
open DotNetLicenses.TestFramework
open TruePath
open Xunit

// REUSE-IgnoreStart

[<Fact>]
let ``NuSpec file path should be determined correctly``(): Task = task {
    let nuGetPackagesRootPath = AbsolutePath "/"
    let package = "FVNever.DotNetLicenses"
    let version = "1.0.0"
    let expectedPath =
        nuGetPackagesRootPath /
        package.ToLowerInvariant() /
        version.ToLowerInvariant() /
        (package.ToLowerInvariant() + ".nuspec")
    let! actualPath =
        GetNuSpecFilePath [| nuGetPackagesRootPath |] {
            PackageId = package
            Version = version
        } RootResolveBehavior.Any
    Assert.Equal(Some expectedPath, actualPath)
}

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
let ``Package file searcher works correctly``(): Task = task {
    use mockedPackageRoot = DisposableDirectory.Create()
    let sourceRoots = [| mockedPackageRoot.Path |]
    let coords = {
        PackageId = "FVNever.DotNetLicenses.StubPackage"
        Version = "1.0.0"
    }
    let fileRelativePath = "content/file.txt"

    use projectDir = DisposableDirectory.Create()
    let projectFile = projectDir.Path / "project.csproj"
    let fileContent = "Hello"B
    let contentFullPath = projectDir.Path / fileRelativePath
    let fileEntry = FileSourceEntry(projectDir.Path, PackageSpecs.Directory projectDir.Path, contentFullPath)

    let deployFileTo(fullPath: AbsolutePath) =
        Directory.CreateDirectory(fullPath.Value |> Path.GetDirectoryName) |> ignore
        File.WriteAllBytesAsync(fullPath.Value, fileContent)

    use cache = new FileHashCache()
    let reference = NuGetReference(projectFile, coords)
    do! deployFileTo contentFullPath
    do! deployFileTo(UnpackedPackagePath mockedPackageRoot.Path reference / "file.txt")

    let! contains = ContainsFile cache sourceRoots (reference, fileEntry)
    Assert.True(contains, "File should be considered as part of the package while it is unchanged.")

    // Sadly, the file system time storage precision is not enough for the test to work without this in some
    // cases. This check is only the best effort anyway, and we can't really suggest to rely on it in production,
    // so it should work for us.
    //
    // This should make the previous file write time distinct enough from this write, for the file's change
    // timestamp to differ between the write operations.
    do! Task.Delay(TimeSpan.FromMilliseconds 1.0)
    File.WriteAllBytes((projectDir.Path / fileRelativePath).Value, "Hello2"B)

    let! contains = ContainsFile cache sourceRoots (reference, fileEntry)
    Assert.False(contains, "File should not be considered as part of the package after change.")
}

[<Fact>]
let ``NuGet reads transitive package references correctly``(): Task =
    DataFiles.Deploy "TestWithTransitiveRef.csproj" (fun project -> task {
        let! _ = MsBuild.ExecuteDotNet [| "restore"; project.Value |]
        let projectAssetsJson = project.Parent.Value / "obj/project.assets.json"
        Assert.True <| File.Exists projectAssetsJson.Value
        let! references = ReadPackageReferences(project, ReferenceType.PackageReference ||| ReferenceType.ProjectAssets)
        Assert.Equal<PackageReference>([|
            NuGetReference(project, { PackageId = "FSharp.Core"; Version = "8.0.200" })
            NuGetReference(project, { PackageId = "Generaptor"; Version = "1.2.0" })
            NuGetReference(project, { PackageId = "YamlDotNet";  Version = "15.1.1" })
        |], references)
    })

// REUSE-IgnoreEnd
