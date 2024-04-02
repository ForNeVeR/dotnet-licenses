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
        Overrides = null
    }, configuration)
}

[<Fact>]
let ``Metadata overrides should be read correctly``(): Task = task {
    let content = """
inputs = ["File.csproj"]
overrides = [
    { id = "Package1", version = "1.0.0", spdx = "MIT", copyright = "" },
    { id = "Package1", version = "2.0.0", spdx = "MIT", copyright = "Copyright1" }
]
"""
    use input = new MemoryStream(Encoding.UTF8.GetBytes content)
    let! configuration = Configuration.Read(input, Some "<test>")
    Assert.Equal({
        Inputs = [| "File.csproj" |]
        Overrides = [|
            {
                Id = "Package1"
                Version = "1.0.0"
                Spdx = "MIT"
                Copyright = ""
            }
            {
                Id = "Package1"
                Version = "2.0.0"
                Spdx = "MIT"
                Copyright = "Copyright1"
            }
        |]
    }, configuration)
}

[<Fact>]
let ``GetOverrides works on duplicate keys``(): Task = task {
    let content = """
inputs = []
overrides = [
    { id = "Package1", version = "1.0.0", spdx = "MIT", copyright = "" },
    { id = "Package2", version = "1.0.0", spdx = "MIT", copyright = "Copyright1" }
]
"""
    use input = new MemoryStream(Encoding.UTF8.GetBytes content)
    let! configuration = Configuration.Read(input, Some "<test>")
    let overrides = configuration.GetOverrides()
    Assert.Equivalent(Map.ofArray [|
        { PackageId = "Package1"; Version = "1.0.0" }, { SpdxExpression = "MIT"; Copyright = "" }
        { PackageId = "Package2"; Version = "1.0.0" }, { SpdxExpression = "MIT"; Copyright = "Copyright1" }
    |], overrides)
}


[<Fact>]
let ``GetOverrides fails on duplicate keys``(): Task = task {
    let content = """
inputs = []
overrides = [
    { id = "Package1", version = "1.0.0", spdx = "MIT", copyright = "" },
    { id = "Package1", version = "1.0.0", spdx = "MIT", copyright = "Copyright1" }
]
"""
    use input = new MemoryStream(Encoding.UTF8.GetBytes content)
    let! configuration = Configuration.Read(input, Some "<test>")
    let ex = Assert.Throws(fun() -> configuration.GetOverrides() |> ignore)
    Assert.Contains("Duplicate key", ex.Message)
}
