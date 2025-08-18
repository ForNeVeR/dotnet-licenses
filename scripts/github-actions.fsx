// SPDX-FileCopyrightText: 2024-2025 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

#r "nuget: Generaptor.Library, 1.8.0"

open System

open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
open type Generaptor.Library.Actions
open type Generaptor.Library.Patterns

let mainBranch = "main"
let linuxImage = "ubuntu-24.04"
let images = [
    "macos-14"
    linuxImage
    "windows-2022"
]

let workflows = [
    let mainTriggers = [
        onPushTo mainBranch
        onPullRequestTo mainBranch
        onSchedule(day = DayOfWeek.Saturday)
        onWorkflowDispatch
    ]

    let pwsh(name, run) = step(name = name, shell = "pwsh", run = run)

    workflow "main" [
        name "Main"
        yield! mainTriggers

        job "verify-workflows" [
            runsOn "ubuntu-24.04"

            setEnv "DOTNET_CLI_TELEMETRY_OPTOUT" "1"
            setEnv "DOTNET_NOLOGO" "1"
            setEnv "NUGET_PACKAGES" "${{ github.workspace }}/.github/nuget-packages"

            step(
                name = "Check out the sources",
                usesSpec = Auto "actions/checkout"
            )
            step(
                name = "Set up .NET SDK",
                usesSpec = Auto "actions/setup-dotnet"
            )
            step(
                name = "Cache NuGet packages",
                usesSpec = Auto "actions/cache",
                options = Map.ofList [
                    "key", "${{ runner.os }}.nuget.${{ hashFiles('**/*.*proj', '**/*.props') }}"
                    "path", "${{ env.NUGET_PACKAGES }}"
                ]
            )

            step(run = "dotnet fsi ./scripts/github-actions.fsx verify")
        ]

        job "test" [
            checkout
            yield! dotNetBuildAndTest()
        ] |> addMatrix images

        job "licenses" [
            runsOn linuxImage
            checkout
            pwsh("Install REUSE", "pipx install reuse")
            pwsh("Check REUSE compliance", "reuse lint")
        ]

        job "encoding" [
            runsOn linuxImage
            checkout
            step(name = "Verify encoding", shell = "pwsh", run = "scripts/Test-Encoding.ps1")
        ]
    ]
    workflow "release" [
        name "Release"
        yield! mainTriggers
        onPushTags "v*"
        job "nuget" [
            runsOn linuxImage
            checkout
            writeContentPermissions

            let configuration = "Release"

            let versionStepId = "version"
            let versionField = "${{ steps." + versionStepId + ".outputs.version }}"
            getVersionWithScript(stepId = versionStepId, scriptPath = "scripts/Get-Version.ps1")

            pwsh("Download licenses", "dotnet run --project DotNetLicenses -- download-licenses .dotnet-licenses.toml")
            dotNetPack(version = versionField)
            pwsh("Verify package", "dotnet run --project DotNetLicenses -- verify .dotnet-licenses.toml")
            pwsh("Verify package metadata", "scripts/Test-NuGetMetadata.ps1")

            let releaseNotes = "./release-notes.md"
            step(
                name = "Read changelog",
                uses = "ForNeVeR/ChangelogAutomation.action@v2",
                options = Map.ofList [
                    "output", releaseNotes
                ]
            )

            let projectName = "DotNetLicenses"
            let packageId = "FVNever.DotNetLicenses"
            let artifacts includeSNuPkg = [
                $"./{projectName}/bin/{configuration}/{packageId}.{versionField}.nupkg"
                if includeSNuPkg then $"./{projectName}/bin/{configuration}/{packageId}.{versionField}.snupkg"
            ]
            let allArtifacts = [
                yield! artifacts true
            ]
            uploadArtifacts [
                releaseNotes
                yield! allArtifacts
            ]
            yield! ifCalledOnTagPush [
                createRelease(
                    name = $"dotnet-licenses v{versionField}",
                    releaseNotesPath = releaseNotes,
                    files = allArtifacts
                )
                yield! pushToNuGetOrg "NUGET_TOKEN" (
                    artifacts false
                )
            ]
        ]
    ]
]

exit <| EntryPoint.Process fsi.CommandLineArgs workflows
