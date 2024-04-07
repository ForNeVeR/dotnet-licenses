// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.SourcesTests

open System.IO
open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.Sources
open DotNetLicenses.TestFramework
open Xunit

[<Fact>]
let ``Sources are enumerated for a file-system directory``(): Task = task {
    use directory = DisposableDirectory.Create()
    do! File.WriteAllTextAsync(Path.Combine(directory.Path, "file.txt"), "content")
    Directory.CreateDirectory(Path.Combine(directory.Path, "subdirectory")) |> ignore
    do! File.WriteAllTextAsync(Path.Combine(directory.Path, "subdirectory", "file.txt"), "content")

    let spec = { Type = "file"; Path  = directory.Path }
    let! entries = ReadEntries [| spec |]
    Assert.Equivalent([|
        { Source = spec; Path = Path.Combine(directory.Path, "file.txt") }
        { Source = spec; Path = Path.Combine(directory.Path, "subdirectory", "file.txt") }
    |], entries)
}

[<Fact>]
let ``SourceRelativePath is generated for a file set``(): unit =
    let source = { Type = "file"; Path = Path.GetTempPath() }
    let file = Path.Combine(source.Path, Path.Combine("foo", "file.txt"))
    let entry = { Source = source; Path = file }
    Assert.Equal("foo/file.txt", entry.SourceRelativePath)

[<Fact>]
let ``SourceEntry calculates its hash correctly``(): unit =
    Assert.Fail()
