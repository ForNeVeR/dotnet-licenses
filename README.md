<!--
SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>

SPDX-License-Identifier: MIT
-->

dotnet-licenses [![Status Zero][status-zero]][andivionian-status-classifier] [![NuGet package][nuget.badge]][nuget.page]
===============
dotnet-licenses is a set of tooling to automate following the requirements and inventorying the lists of the open-source licenses used by the projects that chose to publish licensed artifacts alongside their own files.

The general approach is inspired by [REUSE][reuse], but adopted for binary packages.

Different software licenses have different requirements, but most of them require you to bring the copyright information together with any form of distribution.

dotnet-licenses will help you to verify that some license covers every file in your package, and will help the package consumers to determine exactly what file is covered by what.

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
$ dotnet licenses [print] <config-file-path>
```
This command will print the packages used by the configured projects.

```console
$ dotnet licenses generate-lock <config-file-path>
```

The command's exit code is `0` if the tool ran successfully and non-zero if there were any issues, including warnings.

Read about the configuration file expected to be found by the `<config-file-path>` in the section below.

Configuration
-------------
The configuration file format is TOML. The format:
```toml
inputs = [ # required
  "path/to/project1.csproj",
  "path/to/project2.csproj"
]
overrides = [ # optional
  { id = "package1", version = "1.0.0", spdx = "MIT" , copyright = "Copyright"},
  { id = "package2", version = "2.0.0", spdx = "GPL-3.0", copyright = "Copyright" }
]
lock_file = "path/to/lock-file.toml" # required for generate-lock
package = [ # required for generate-lock
    { type = "directory", path = "bin" },
    { type = "zip", path = "bin/*.zip" }
]
```
The `inputs` parameter (required) is a list of paths to the projects to analyze. The paths are either absolute or relative to the directory containing the configuration file.

The `overrides` parameter (optional) should contain a set of license overrides for incorrectly marked packages in NuGet. Every record contains string fields `id`, `version`, `spdx`, and `copyright`. All fields are mandatory.

The `lock_file` parameter (optional) is the path to the license lock file that will be produced or verified by the corresponding commands. The path is either absolute or relative to the directory containing the configuration file. This parameter is mandatory for the `generate-lock` command.

The `package` parameter (optional) describes the list of the files you want to check for their license contents. It is a list of the entries having the following structure:
- `type` (required) should be either `directory` (to point to the root of the file hierarchy that will be processed recursively) or `zip`, to point to a zip archive that the tool will analyze,
- `path` (required) is a path on disk; for zip archives, we support glob patterns.

The `package` parameter is mandatory for the `generate-lock` command.

Lock File
---------
License lock file looks like this:
```toml
[["file_name.txt"]]
source_id = "FSharp.Core"
source_version = "8.0.200"
spdx = "MIT"
copyright = "© Microsoft Corporation. All rights reserved."
```

- `file_name` is the path of the file relatively to the package root. May be a glob in some cases.
- `source_id` is the NuGet package that is the origin of the file.
- `source_version` is the version of the package.
- `spdx` is the SPDX identifier of the license.
- `copyright` is the copyright statement of the license.

One file may have several records in case it is covered by several licenses.

You are meant to commit the lock file and update it if something in the package contents change.

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

Note that the particular non-zero exit codes are not considered part of the public API.

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
