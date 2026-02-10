// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.ConfigurationTests

open System.IO
open System.Text
open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.CommandLine
open DotNetLicenses.TestFramework
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
            LockFile = Some <| LocalPath "foo.toml"
            PackagedFiles = [|
                PackageSpecs.Directory "files"
                PackageSpecs.Zip "files2/*.zip"
            |]
    }

[<Fact>]
let ``Metadata overrides are read correctly``(): Task =
    let content = """
metadata_sources = [{ type = "nuget", include = "File.csproj" }]
metadata_overrides = [
    { id = "Package1", version = "1.0.0", spdx = "MIT", copyright = "" },
    { id = "Package1", version = "2.0.0", spdx = ["MIT"], copyright = ["Copyright1"] }
]
"""
    doTest content {
        Configuration.Empty with
            MetadataSources = [| NuGetSource.Of "File.csproj" |]
            MetadataOverrides = [|
                {
                    Id = "Package1"
                    Version = "1.0.0"
                    Spdx = [|"MIT"|]
                    Copyright = [|""|]
                }
                {
                    Id = "Package1"
                    Version = "2.0.0"
                    Spdx = [|"MIT"|]
                    Copyright = [|"Copyright1"|]
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
                License {
                    Spdx = "MIT"
                    Copyright =  "Me"
                    FilesCovered = [| LocalPathPattern "**/*.txt" |]
                    PatternsCovered = Array.empty
                }
                License {
                    Spdx = "MIT"
                    Copyright =  "Me"
                    FilesCovered = [|
                        LocalPathPattern "**/*.pdf"
                        LocalPathPattern "**/*.pdb"
                    |]
                    PatternsCovered = Array.empty
                }
            |]
    }

[<Fact>]
let ``REUSE source is read correctly``(): Task =
    let content = """
metadata_sources = [
    { type = "reuse", root = "path1" },
    { type = "reuse", root = "path2", exclude = ["path2/exclude"], files_covered = ["README.md"] },
    { type = "reuse", root = "path3", exclude = "path3/exclude", files_covered = "README.md" },
]
"""
    doTest content {
        Configuration.Empty with
            MetadataSources = [|
                ReuseSource.Of "path1"
                Reuse {
                    Root = LocalPath "path2"
                    Exclude = [|LocalPath "path2/exclude"|]
                    FilesCovered = [| LocalPathPattern "README.md" |]
                    PatternsCovered = Array.empty
                }
                Reuse {
                    Root = LocalPath "path3"
                    Exclude = [|LocalPath "path3/exclude"|]
                    FilesCovered = [| LocalPathPattern "README.md" |]
                    PatternsCovered = Array.empty
                }
            |]
    }

[<Fact>]
let ``Ignore presets read correctly``(): Task =
    let content = """
metadata_sources = []
packaged_files = [
    { type = "zip", path = "files2/*.zip", ignore = [
        { type = "preset", name = "licenses" }
    ] },
]
"""
    doTest content {
        Configuration.Empty with
            PackagedFiles = [|
                { PackageSpecs.Zip "files2/*.zip" with Ignores = [| Preset "licenses" |] }
            |]
    }

[<Fact>]
let ``Configuration should throw on unknown ignore pattern``(): Task =
    let content = """
metadata_sources = []
packaged_files = [
    { type = "zip", path = "files2/*.zip", ignore = [
        { type = "preset", name = "unknown" }
    ] },
]
"""
    task {
        use input = new MemoryStream(Encoding.UTF8.GetBytes content)
        let! ex = Assert.ThrowsAsync<InvalidDataException>(fun () -> Configuration.Read(input, Some "<test>"))
        Assert.Equal("Unknown ignore preset \"unknown\".", ex.Message)
    }

[<Fact>]
let ``Covered patterns are read correctly``(): Task =
    let content = """
metadata_sources = [
    { type = "license", spdx = "MIT", copyright = "My Copyright", patterns_covered = [
        { type = "msbuild", include = "project2.csproj" },
        { type = "nuget" },
    ] },
    { type = "reuse", root = ".", patterns_covered = [
        { type = "msbuild", include = "project2.csproj" }
    ] },
]
"""
    doTest content {
        Configuration.Empty with
            MetadataSources = [|
                License {
                    Spdx = "MIT"
                    Copyright = "My Copyright"
                    FilesCovered = Array.empty
                    PatternsCovered = [|
                        MsBuildCoverage(LocalPath "project2.csproj")
                        NuGetCoverage
                    |]
                }
                Reuse {
                    Root = LocalPath "."
                    Exclude = Array.empty
                    FilesCovered = Array.empty
                    PatternsCovered = [|
                        MsBuildCoverage(LocalPath "project2.csproj")
                    |]
                }
            |]
    }

let ``Download location is read correctly``(): Task =
    let content = """
license_storage_path = "../licenses"
"""
    doTest content {
        Configuration.Empty with
            LicenseStorage = Some <| LocalPath "../licenses"
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
        { PackageId = "Package1"; Version = "1.0.0" }, { SpdxExpressions = [|"MIT"|]; Copyrights = [|""|] }
        { PackageId = "Package2"; Version = "1.0.0" }, { SpdxExpressions = [|"MIT"|]; Copyrights = [|"Copyright1"|] }
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
        { PackageId = "Package1"; Version = "1.0.0" }, { SpdxExpressions = [|"MIT"|]; Copyrights = [|"Copyright1"|] }
    |], result)
}
