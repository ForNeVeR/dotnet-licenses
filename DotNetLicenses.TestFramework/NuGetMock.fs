// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.TestFramework.NuGetMock

open System.Threading.Tasks
open DotNetLicenses
open DotNetLicenses.NuGet
open TruePath

let MirroringReader = {
    new INuGetReader with
        member _.ReadNuSpec { PackageId = id; Version = version } = Task.FromResult {
            Metadata = {
                Id = id
                Version = version
                License = {
                    Type = "expression"
                    Value = $"License {id}"
                }
                Copyright = $"Copyright {id}"
            }
        }
        member _.FindFile _ packages _ = packages |> Seq.toArray |> Task.FromResult
}

let WithNuGetPackageRoot (rootPath: AbsolutePath) (action: unit -> Task): Task = task {
    let oldPath = PackagesFolderPath
    try
        PackagesFolderPath <- rootPath
        do! action()
    finally
        PackagesFolderPath <- oldPath
}
