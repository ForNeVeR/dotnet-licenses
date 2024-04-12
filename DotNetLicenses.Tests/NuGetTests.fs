// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.NuGetTests

open System.IO
open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.NuGet
open DotNetLicenses.Sources
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
let ``Package file searcher works correctly``(): Task = task {
    use mockedPackageRoot = DisposableDirectory.Create()
    let package = {
        PackageId = "FVNever.DotNetLicenses.StubPackage"
        Version = "1.0.0"
    }
    let fileRelativePath = "content/file.txt"

    use projectDir = DisposableDirectory.Create()
    let fileContent = "Hello"B
    let contentFullPath = Path.Combine(projectDir.Path, fileRelativePath)
    let fileEntry = FileSourceEntry(Directory projectDir.Path, contentFullPath)

    let deployFileTo(fullPath: string) =
        Directory.CreateDirectory(fullPath |> Path.GetDirectoryName) |> ignore
        File.WriteAllBytesAsync(fullPath, fileContent)

    use cache = new FileHashCache()
    do! NuGetMock.WithNuGetPackageRoot mockedPackageRoot.Path <| fun() -> task {
        do! deployFileTo contentFullPath
        do! deployFileTo(Path.Combine(UnpackedPackagePath package, "file.txt"))

        let! contains = ContainsFile cache (package, fileEntry)
        Assert.True(contains, "File should be considered as part of the package while it is unchanged.")

        File.WriteAllBytes(Path.Combine(projectDir.Path, fileRelativePath), "Hello2"B)

        let! contains = ContainsFile cache (package, fileEntry)
        Assert.False(contains, "File should not be considered as part of the package after change.")
    }
}
