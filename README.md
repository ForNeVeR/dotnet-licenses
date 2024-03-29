<!--
SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>

SPDX-License-Identifier: MIT
-->

dotnet-licenses
===============
dotnet-licenses is a set of tooling to maintain the software license information in published packages.

The general approach is inspired by [REUSE][reuse], but adopted for binary packages.

Different software licenses have different requirements, but most of them require you to bring the copyright information together with any form of distribution.

dotnet-licenses will help you to verify that every file in your package is covered by a license, and will help the package consumers to determine exactly what file is covered by what.

Installation
------------
Install as a dotnet tool: either
```console
$ dotnet tool install --global FVNever.DotNetLicenses
```
for global installation or
```console
$ dotnet new tool-manifest
$ dotnet tool install FVNever.DotNetLicenses
```
for local solution-wide installation.

Usage
-----
To run the tool, use the following shell command:
```console
$ dotnet licenses
```

Documentation
-------------
- [Changelog][docs.changelog]
- [Contributor Guide][docs.contributing]
- [Maintainer Guide][docs.maintaining]

License
-------
This project's licensing follows the [REUSE specification v 3.0][reuse.spec]. The main license for the source is MIT, consult each file's headers and the REUSE specification for possible details.

<!-- TODO: We should use the tool itself to deliver this license, obviously. -->
The package of this program bundles FSharp.Core, see its license (MIT) [on the NuGet page][fsharp.core.nuget].

[docs.changelog]: CHANGELOG.md
[docs.contributing]: CONTRIBUTING.md
[docs.maintaining]: MAINTAINING.md
[fsharp.core.nuget]: https://www.nuget.org/packages/FSharp.Core/
[reuse.spec]: https://reuse.software/spec/
[reuse]: https://reuse.software/
