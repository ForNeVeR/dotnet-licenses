// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.ConfigurationTests

open System.IO
open System.Text
open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.CommandLine
open TruePath
open Xunit

let private doTest (content: string) (expected: Configuration) =
    task {
        use input = new MemoryStream(Encoding.UTF8.GetBytes content)
        let! configuration = Configuration.Read(input, Some "<test>")
        Assert.Equal(expected, configuration)
    }

[<Fact>]
let ``Configuration should be read correctly``(): Task =
    let content = """
metadata_sources = [
  { type = "nuget", include = "File.csproj" },
  { type = "nuget", include = "File.fsproj" },
]
lock_file = "foo.toml"
packaged_files = [
    { type = "directory", path = "files" },
    { type = "zip", path = "files2/*.zip" },
]
"""
    doTest content {
        Configuration.Empty with
            MetadataSources = [|
                NuGetSource.Of "File.csproj"
                NuGetSource.Of "File.fsproj"
            |]
            LockFile = Some <| RelativePath "foo.toml"
            PackagedFiles = [|
                Directory <| RelativePath "files"
                Zip <| LocalPathPattern "files2/*.zip"
            |]
    }

[<Fact>]
let ``Metadata overrides are read correctly``(): Task =
    let content = """
metadata_sources = [{ type = "nuget", include = "File.csproj" }]
metadata_overrides = [
    { id = "Package1", version = "1.0.0", spdx = "MIT", copyright = "" },
    { id = "Package1", version = "2.0.0", spdx = "MIT", copyright = "Copyright1" }
]
"""
    doTest content {
        Configuration.Empty with
            MetadataSources = [| NuGetSource.Of "File.csproj" |]
            MetadataOverrides = [|
                {
                    Id = "Package1"
                    Version = "1.0.0"
                    Spdx = "MIT"
                    Copyright = ""
                }
                {
                    Id = "Package1"
                    Version = "2.0.0"
                    Spdx = "MIT"
                    Copyright = "Copyright1"
                }
            |]
    }

[<Fact>]
let ``License metadata source is read correctly``(): Task =
    let content = """
metadata_sources = [
    { type = "license", spdx = "MIT", copyright = "Me", files_covered = "**/*.txt" },
    { type = "license", spdx = "MIT", copyright = "Me", files_covered = ["**/*.pdf", "**/*.pdb"] },
]
"""
    doTest content {
        Configuration.Empty with
            MetadataSources = [|
                License { Spdx = "MIT"; Copyright =  "Me"; FilesCovered = [| LocalPathPattern "**/*.txt" |] }
                License { Spdx = "MIT"; Copyright =  "Me"; FilesCovered = [|
                    LocalPathPattern "**/*.pdf"
                    LocalPathPattern "**/*.pdb"
                |] }
            |]
    }

[<Fact>]
let ``GetOverrides works on duplicate package names (not versions)``(): Task = task {
    let content = """
metadata_sources = []
metadata_overrides = [
    { id = "Package1", version = "1.0.0", spdx = "MIT", copyright = "" },
    { id = "Package2", version = "1.0.0", spdx = "MIT", copyright = "Copyright1" }
]
"""
    use input = new MemoryStream(Encoding.UTF8.GetBytes content)
    let! configuration = Configuration.Read(input, Some "<test>")
    let wp = WarningProcessor()
    let overrides = configuration.GetMetadataOverrides wp
    Assert.Equivalent(Map.ofArray [|
        { PackageId = "Package1"; Version = "1.0.0" }, { SpdxExpression = "MIT"; Copyright = "" }
        { PackageId = "Package2"; Version = "1.0.0" }, { SpdxExpression = "MIT"; Copyright = "Copyright1" }
    |], overrides)
    Assert.Equivalent(WarningProcessor(), wp)
}


[<Fact>]
let ``GetOverrides prints a warning on duplicate keys``(): Task = task {
    let content = """
metadata_sources = []
metadata_overrides = [
    { id = "Package1", version = "1.0.0", spdx = "MIT", copyright = "" },
    { id = "Package1", version = "1.0.0", spdx = "MIT", copyright = "Copyright1" }
]
"""
    use input = new MemoryStream(Encoding.UTF8.GetBytes content)
    let! configuration = Configuration.Read(input, Some "<test>")
    let wp = WarningProcessor()
    let result = configuration.GetMetadataOverrides wp
    Assert.Equal(ExitCode.DuplicateOverride, Assert.Single wp.Codes)
    Assert.Equal<string>(
        [| "Duplicate key in the metadata_overrides collection: id = \"Package1\", version = \"1.0.0\"." |],
        wp.Messages
    )
    Assert.Equivalent(Map.ofArray [|
        { PackageId = "Package1"; Version = "1.0.0" }, { SpdxExpression = "MIT"; Copyright = "Copyright1" }
    |], result)
}
