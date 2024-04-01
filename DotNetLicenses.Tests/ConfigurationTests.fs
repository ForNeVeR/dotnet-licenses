// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.ConfigurationTests

open System.IO
open System.Text
open System.Threading.Tasks
open DotNetLicenses
open Xunit

[<Fact>]
let ``Configuration should be read correctly``(): Task = task {
    let content = """
inputs = [
  "File.csproj",
  "File.fsproj",
]
"""
    use input = new MemoryStream(Encoding.UTF8.GetBytes content)
    let! configuration = Configuration.Read(input, Some "<test>")
    Assert.Equal({
        Inputs = [|
            "File.csproj"
            "File.fsproj"
        |]
    }, configuration)
}
