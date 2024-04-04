// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.TestFramework.NuGetMock

open System.Threading.Tasks
open DotNetLicenses.NuGet

let MirroringReader = {
    new INuGetReader with
        member this.ReadNuSpec { PackageId = id } = Task.FromResult {
            Metadata = {
                Id = id
                License = {
                    Type = "expression"
                    Value = $"License {id}"
                }
                Copyright = $"Copyright {id}"
            }
        }
}
