// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.CoveragePatternTests

open System.IO
open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.CoveragePattern
open DotNetLicenses.LockFile
open DotNetLicenses.Metadata
open DotNetLicenses.TestFramework
open TruePath
open Xunit

// REUSE-IgnoreStart
[<Fact>]
let ``MSBuild coverage pattern collector works``(): Task =
    DataFiles.Deploy "Test.csproj" (fun project -> task {
        let metadata = MetadataItem.License {
            Spdx = "MIT"
            Copyright = "2024 Me"
            FilesCovered = Array.empty
            PatternsCovered = [| MsBuildCoverage(LocalPath project) |]
        }

        let! projectOutput = MsBuild.GetProjectGeneratedArtifacts project
        let baseDirectory = projectOutput.Parent.Value
        Directory.CreateDirectory baseDirectory.Value |> ignore
        do! File.WriteAllTextAsync(projectOutput.Value, "test output")
        let packageSpec = {
            Source = Directory <| LocalPath(projectOutput.Parent.Value)
            Ignores = Array.empty
        }

        let sourceEntry = {
            new ISourceEntry with
                member _.Source = packageSpec
                member _.SourceRelativePath = projectOutput.FileName
                member _.ReadContent() = Task.FromResult <| File.OpenRead projectOutput.Value
                member this.CalculateHash() = task {
                    let! content = File.ReadAllBytesAsync projectOutput.Value
                    return Hashes.Sha256 content
                }
        }

        let coverageCache = CoverageCache.Empty()
        use hashCache = new FileHashCache()
        let! result = CollectCoveredFileLicense baseDirectory coverageCache hashCache [| metadata |] sourceEntry
        Assert.Equal<_>([| LocalPathPattern sourceEntry.SourceRelativePath, {
            SourceId = null
            SourceVersion = null
            Spdx = [|"MIT"|]
            Copyright = [|"2024 Me"|]
        } |], result)
    })

[<Fact>]
let ``NuGet coverage pattern collector works``(): Task = task {
    use directory = DisposableDirectory.Create()
    let files = [|
        directory.Path / "[Content_Types].xml"
        directory.Path / "_rels/.rels"
        directory.Path / "package/services/metadata/core-properties/random.psmdcp"
    |]
    for file in files do
        Directory.CreateDirectory file.Parent.Value.Value |> ignore
        do! File.WriteAllTextAsync(file.Value, "test output")

    let metadata = MetadataItem.License {
        Spdx = "MIT"
        Copyright = "2024 Me"
        FilesCovered = Array.empty
        PatternsCovered = [| NuGetCoverage |]
    }
    let packageSpec = {
        Source = Directory <| LocalPath(directory.Path)
        Ignores = Array.empty
    }
    let sourceEntries = files |> Seq.map (fun file -> {
        new ISourceEntry with
            member _.Source = packageSpec
            member _.SourceRelativePath =
                LocalPath(file)
                    .RelativeTo(LocalPath directory.Path)
                    .ToString().Replace(Path.DirectorySeparatorChar, '/')
            member _.ReadContent() = Task.FromResult <| File.OpenRead file.Value
            member this.CalculateHash() = task {
                let! content = File.ReadAllBytesAsync file.Value
                return Hashes.Sha256 content
            }
    })

    let coverageCache = CoverageCache.Empty()
    use hashCache = new FileHashCache()

    for entry in sourceEntries do
        let! result = CollectCoveredFileLicense directory.Path coverageCache hashCache [| metadata |] entry
        let pattern =
            if entry.SourceRelativePath.StartsWith("package/services/metadata/core-properties")
            then "package/services/metadata/core-properties/*.psmdcp"
            else entry.SourceRelativePath
            |> LocalPathPattern
        Assert.Equal<_>([| pattern, {
            SourceId = null
            SourceVersion = null
            Spdx = [|"MIT"|]
            Copyright = [|"2024 Me"|]
        } |], result)
}
// REUSE-IgnoreEnd
