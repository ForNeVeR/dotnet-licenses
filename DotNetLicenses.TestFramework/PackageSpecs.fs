// SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.md>
//
// SPDX-License-Identifier: MIT

namespace DotNetLicenses.TestFramework

open DotNetLicenses
open DotNetLicenses.Ignores
open TruePath

type PackageSpecs =
    static member Zip(pattern: string) = { Source = Zip <| LocalPathPattern pattern; Ignores = DefaultPresets }
    static member Directory(path: string) = { Source = Directory <| LocalPath path; Ignores = DefaultPresets }
    static member Directory(path: AbsolutePath) =
        { Source = Directory <| LocalPath.op_Implicit path; Ignores = DefaultPresets }
