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
        member _.ReadNuSpec _ { PackageId = id; Version = version } = Task.FromResult <| Some {
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

        member _.ContainsFileName _ _ _ = Task.FromResult true

        member _.FindFile _ packages _ = packages |> Seq.toArray |> Task.FromResult

let MirroringReader = MockedNuGetReader None

type DirectNuSpecReader(packages: AbsolutePath) =
    let parent = NuGetReader() :> INuGetReader
    interface INuGetReader with
        member _.ReadNuSpec _ coords = task {
            let! nuSpec = GetNuSpecFilePath [| packages |] coords RootResolveBehavior.Any
            let! result = ReadNuSpec(Option.get nuSpec)
            return Some result
        }
        member _.ReadPackageReferences path = parent.ReadPackageReferences path
        member _.ContainsFileName path name packages = parent.ContainsFileName path name packages
        member _.FindFile path packages name = parent.FindFile path packages name
