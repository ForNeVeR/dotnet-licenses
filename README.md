<!--
SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>

SPDX-License-Identifier: MIT
-->

dotnet-licenses [![Status Zero][status-zero]][andivionian-status-classifier] [![NuGet package][nuget.badge]][nuget.page]
===============
dotnet-licenses is a set of tooling to automate following the requirements and inventorying the lists of the open-source licenses used by the projects that chose to publish licensed artifacts alongside their own files.

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
$ dotnet licenses <config-file-path>
```
This command will print the packages used by the configured projects.

The command's exit code is 0 if the tool ran successfully and 1 if there were any issues, including warnings.

Configuration
-------------
The configuration file format is TOML. The format:
```toml
inputs = [
  "path/to/project1.csproj",
  "path/to/project2.csproj"
]
overrides = [
  { id = "package1", version = "1.0.0", spdx = "MIT" , copyright = "Copyright"},
  { id = "package2", version = "2.0.0", spdx = "GPL-3.0", copyright = "Copyright" }
]
```
The `inputs` record is a list of paths to the projects to analyze. The paths are either absolute or relative to the directory containing the configuration file.

The `overrides` record (optional) should contain a set of license overrides for incorrectly marked packages in NuGet. Every record contains string fields `id`, `version`, `spdx`, and `copyright`. All fields are mandatory.

Documentation
-------------
- [Changelog][docs.changelog]
- [Core Concepts][docs.concepts]
- [Contributor Guide][docs.contributing]
- [Maintainer Guide][docs.maintaining]

Versioning Notes
----------------
This project's versioning follows the [Semantic Versioning 2.0.0][semver] specification.

When considering compatible changes, we currently consider the project's public API is the command-line interface:
- the way of running the project (e.g., the executable file name),
- the input arguments,
- the input data formats,
- and the output data format.

License
-------
This project's licensing follows the [REUSE specification v 3.0][reuse.spec]. The main license for the source is MIT, consult each file's headers and the REUSE specification for possible details.

<!-- TODO[#6]: We should use the tool itself to deliver this license, obviously. -->
The package of this program bundles FSharp.Core, see its license (MIT) [on the NuGet page][fsharp.core.nuget].

[andivionian-status-classifier]: https://andivionian.fornever.me/v1/#status-zero-
[docs.changelog]: CHANGELOG.md
[docs.concepts]: docs/concepts.md
[docs.contributing]: CONTRIBUTING.md
[docs.maintaining]: MAINTAINING.md
[fsharp.core.nuget]: https://www.nuget.org/packages/FSharp.Core/
[nuget.badge]: https://img.shields.io/nuget/v/FVNever.DotNetLicenses
[nuget.page]: https://www.nuget.org/packages/FVNever.DotNetLicenses
[reuse.spec]: https://reuse.software/spec/
[reuse]: https://reuse.software/
[semver]: https://semver.org/spec/v2.0.0.html
[status-zero]: https://img.shields.io/badge/status-zero-lightgrey.svg
