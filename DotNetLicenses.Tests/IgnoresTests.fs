// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.md>
//
// SPDX-License-Identifier: MIT

module DotNetLicenses.Tests.IgnoresTests

open DotNetLicenses
open Xunit

[<Fact>]
let ``License filter should work``(): unit =
    let presets = [|Ignores.Licenses|]
    let makeItem relativePath = {
        new ISourceEntry with
            override this.CalculateHash() = failwith "Should not be called."
            override this.ReadContent() = failwith "Should not be called."
            override this.SourceRelativePath = relativePath
            override this.Source = failwith "Should not be called."
    }

    let foo = makeItem "foo.txt"
    let license1 = makeItem "LICENSES/MIT.txt"
    let license2 = makeItem "licenses/MIT.txt"
    Assert.Equal([| foo |], Ignores.Filter presets [| foo; license1; license2 |])
