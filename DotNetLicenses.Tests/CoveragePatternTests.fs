// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.CoveragePatternTests

open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.TestFramework
open Xunit

// REUSE-IgnoreStart
[<Fact>]
let ``Coverage pattern collector works``(): Task =
    DataFiles.Deploy "Test.csproj" (fun project ->
        let metadata = License {
            Spdx = "MIT"
            Copyright = "2024 Me"
            FilesCovered = Array.empty
            PatternsCovered = MsBuildCoveragePattern <| LocalPath project
        }

        let! projectOutput = MsBuild.GetProjectGeneratedArtifacts project
        do! File.WriteAllTextAsync(projectOutput.Value, "test output")
        let packageSpec = {
            Source = Directory projectOutput.Parent
            Ignores = Array.empty
        }

        let sourceEntry = {
            new ISourceEntry with
                member _.Source = packageSpec
                member _.SourceRelativePath = LocalPath projectOutput.FileName
                member _.ReadContent = Task.FromResult <| File.OpenStream projectOutput.Value
                member this.CalculateHash = task {
                    use! stream = this.ReadContent()
                    let! content = stream.ReadAllBytesAsync()
                    return Hashes.Sha256 content
                }
        }

        let! result = CoveragePattern.CollectCoveredFileLicense [| metadata |] sourceEntry
        Assert.Equal([| {
            SourceId = null
            SourceVersion = null
            Spdx = "MIT"
            Copyright = "2024 Me"
        } |], result)
    )
// REUSE-IgnoreEnd
