// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

#r "nuget: Generaptor.Library, 1.1.0"

open System

open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
open type Generaptor.Library.Actions
open type Generaptor.Library.Patterns

let mainBranch = "main"
let linuxImage = "ubuntu-22.04"
let images = [
    "macos-12"
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

    workflow "main" [
        name "Main"
        yield! mainTriggers
        job "main" [
            checkout
            yield! dotNetBuildAndTest()
        ] |> addMatrix images
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
            dotNetPack(version = versionField)

            let releaseNotes = "./release-notes.md"
            prepareChangelog(releaseNotes)
            let artifacts projectName includeSNuPkg = [
                $"./{projectName}/bin/{configuration}/{projectName}.{versionField}.nupkg"
                if includeSNuPkg then $"./{projectName}/bin/{configuration}/{projectName}.{versionField}.snupkg"
            ]
            let allArtifacts = [
                yield! artifacts "DotNetLicenses" true
            ]
            uploadArtifacts [
                releaseNotes
                yield! allArtifacts
            ]
            yield! ifCalledOnTagPush [
                createRelease(
                    name = $"dotnet-licenses {versionField}",
                    releaseNotesPath = releaseNotes,
                    files = allArtifacts
                )
                yield! pushToNuGetOrg "NUGET_TOKEN" (
                    artifacts "DotNetLicenses" false
                )
            ]
        ]
    ]
]


EntryPoint.Process fsi.CommandLineArgs workflows
