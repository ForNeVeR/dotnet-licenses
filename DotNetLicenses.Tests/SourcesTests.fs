// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.SourcesTests

open System
open System.IO
open System.IO.Compression
open System.Security.Cryptography
open System.Text
open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.Sources
open DotNetLicenses.TestFramework
open JetBrains.Lifetimes
open Xunit

[<Fact>]
let ``Sources are enumerated for a file-system directory``(): Task = task {
    use directory = DisposableDirectory.Create()
    do! File.WriteAllTextAsync((directory.Path / "file.txt").Value, "content")
    Directory.CreateDirectory (directory.Path / "subdirectory").Value |> ignore
    do! File.WriteAllTextAsync((directory.Path / "subdirectory" / "file.txt").Value, "content")

    let spec = PackageSpec.Directory directory.Path
    use ld = new LifetimeDefinition()
    let! entries = ReadEntries ld.Lifetime directory.Path [| spec |]
    Assert.Equivalent([|
        FileSourceEntry(directory.Path, spec, directory.Path / "file.txt")
        FileSourceEntry(directory.Path, spec, directory.Path / "subdirectory" / "file.txt")
    |], entries)
}

[<Fact>]
let ``SourceRelativePath is generated for a file set``(): unit =
    use directory = DisposableDirectory.Create()
    let source = PackageSpec.Directory directory.Path
    let file = directory.Path / "foo" / "file.txt"
    let entry = FileSourceEntry(directory.Path, source, file) :> ISourceEntry
    Assert.Equal("foo/file.txt", entry.SourceRelativePath)

[<Fact>]
let ``SourceEntry calculates its hash correctly``(): Task = task {
    let calculateSha256(x: byte[]) =
        use hash = SHA256.Create()
        hash.ComputeHash x |> Convert.ToHexString

    use directory = DisposableDirectory.Create()
    let content = "Hello"B
    let expectedHash = calculateSha256 content
    let path = directory.Path / "file.txt"
    File.WriteAllBytes(path.Value, content)
    let source = PackageSpec.Directory directory.Path
    let entry = FileSourceEntry(directory.Path, source, path) :> ISourceEntry
    let! actualHash = entry.CalculateHash()
    Assert.Equal(expectedHash, actualHash)
}

[<Fact>]
let ``SourceEntry reads contents from a ZIP archive``(): Task = task {
    let content = "Hello"
    let fileName = "file.txt"
    use directory = DisposableDirectory.Create()
    let archivePath = directory.Path / "archive.zip"
    ZipFiles.SingleFileArchive(archivePath, fileName, Encoding.UTF8.GetBytes content)
    use stream = File.OpenRead archivePath.Value
    use archive = new ZipArchive(stream)
    let source = PackageSpec.Zip archivePath.Value
    let entry = ZipSourceEntry(source, archive, archivePath, fileName) :> ISourceEntry
    use! stream = entry.ReadContent()
    use reader = new StreamReader(stream)
    let! text = reader.ReadToEndAsync()
    Assert.Equal(content, text)
}

[<Fact>]
let ``Ignore pattern works as expected``(): unit =
    Assert.Fail()
