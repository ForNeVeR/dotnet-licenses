// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.LockFileTests

open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open DotNetLicenses.LockFile
open DotNetLicenses.TestFramework
open TruePath
open Xunit

[<Fact>]
let ``LockFile should be have expected format``(): Task =
    let item id = {
        LockFileItem.SourceId = Some id
        SourceVersion = Some "1.0.0"
        Spdx = [|"MIT"|]
        Copyright = [|"none"|]
        IsIgnored = false
    }
    let data = [|
        LocalPathPattern "x.txt", item "a"
        LocalPathPattern "x.txt", item "b"
        LocalPathPattern "y.txt", item "a"
        LocalPathPattern "a.txt", item "a"
    |]

    let expectedContent = """# REUSE-IgnoreStart
"a.txt" = [{source_id = "a", source_version = "1.0.0", spdx = ["MIT"], copyright = ["none"]}]
"x.txt" = [{source_id = "a", source_version = "1.0.0", spdx = ["MIT"], copyright = ["none"]}, {source_id = "b", source_version = "1.0.0", spdx = ["MIT"], copyright = ["none"]}]
"y.txt" = [{source_id = "a", source_version = "1.0.0", spdx = ["MIT"], copyright = ["none"]}]
# REUSE-IgnoreEnd
"""
    task {
        let lockFilePath = AbsolutePath(Path.GetTempFileName())
        do! SaveLockFile(lockFilePath, data)
        let! content = File.ReadAllTextAsync lockFilePath.Value
        Assert.Equal(expectedContent.ReplaceLineEndings "\n", content.ReplaceLineEndings "\n")
    }

[<Fact>]
let ``LockFile is read correctly``(): Task = task {
    use dir = DisposableDirectory.Create()
    let content = """# REUSE-IgnoreStart
"a.txt" = [{source_id = "a", source_version = "1.0.0", spdx = ["MIT"], copyright = ["none"]}]
"x.txt" = [{source_id = "a", source_version = "1.0.0", spdx = ["MIT"], copyright = ["none"]}, {source_id = "b", source_version = "1.0.0", spdx = ["MIT"], copyright = ["none"]}]
"y.txt" = [{source_id = "a", source_version = "1.0.0", spdx = ["MIT"], copyright = ["none"]}]
# REUSE-IgnoreEnd
"""
    let filePath = dir.Path / "lock.toml"
    do! File.WriteAllTextAsync(filePath.Value, content)
    let! data = ReadLockFile filePath
    let expected = Dictionary()
    expected.Add(
        LocalPathPattern "a.txt",
        [|{
            LockFileItem.SourceId = Some "a"
            SourceVersion = Some "1.0.0"
            Spdx = [|"MIT"|]
            Copyright = [|"none"|]
            IsIgnored = false
        }|]
    )
    expected.Add(
        LocalPathPattern "x.txt",
        [|
            {
                LockFileItem.SourceId = Some "a"
                SourceVersion = Some "1.0.0"
                Spdx = [|"MIT"|]
                Copyright = [|"none"|]
                IsIgnored = false
            };
            {
                LockFileItem.SourceId = Some "b"
                SourceVersion = Some "1.0.0"
                Spdx = [|"MIT"|]
                Copyright = [|"none"|]
                IsIgnored = false
            }
        |]
    )
    expected.Add(
        LocalPathPattern "y.txt",
        [|{
            LockFileItem.SourceId = Some "a"
            SourceVersion = Some "1.0.0"
            Spdx = [|"MIT"|]
            Copyright = [|"none"|]
            IsIgnored = false
        }|]
    )
    Assert.Equivalent(expected, data)
}
