// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.TestFramework.NuGetMock

open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.NuGet
open TruePath

type MockedNuGetReader(licenseOverride: string option) =
    interface INuGetReader with
        member _.ReadNuSpec { PackageId = id; Version = version } = Task.FromResult <| Some {
            Metadata = {
                Id = id
                Version = version
                License = {
                    Type = "expression"
                    Value = licenseOverride |> Option.defaultWith (fun () -> $"License {id}")
                }
                Copyright = $"Copyright {id}"
            }
        }

        member _.ReadPackageReferences(path: AbsolutePath) =
            ReadPackageReferences(path, ReferenceType.PackageReference)

        member _.ContainsFileName _ _ = Task.FromResult true

        member _.FindFile _ packages _ = packages |> Seq.toArray |> Task.FromResult

let MirroringReader = MockedNuGetReader None

let WithNuGetPackageRoot (rootPath: AbsolutePath) (action: unit -> Task): Task = task {
    let oldPath = PackagesFolderPath
    try
        PackagesFolderPath <- rootPath
        do! action()
    finally
        PackagesFolderPath <- oldPath
}
