<!--
SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>

SPDX-License-Identifier: MIT

REUSE-IgnoreStart
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
$ dotnet licenses [print-packages] <config-file-path>
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
metadata_sources = [# required
    { type = "nuget", include = "path/to/project1.sln/csproj" },
    { type = "license", spdx = "MIT", copyright = "My Copyright", files_covered = "*" },
    { type = "license", spdx = "MIT", copyright = "My Other Copyright", files_covered = [
        # You can also include lists into this field.
        "*.txt",
        "*.css"
    ], patterns_covered = [
        { type = "msbuild", include = "path/to/project2.csproj" },
        { type = "nuget" }
    ] },
    { type = "reuse", root = ".", exclude = [".idea/"], files_covered = "README.md" },
]
metadata_overrides = [# optional
    { id = "package1", version = "1.0.0", spdx = "MIT", copyright = "Copyright" },
    { id = "package2", version = "2.0.0", spdx = "GPL-3.0", copyright = "Copyright" }
]
lock_file = "path/to/lock-file.toml" # required for generate-lock
packaged_files = [# required for generate-lock
    { type = "directory", path = "bin", ignore = [
        { type = "preset", name = "licenses" }
    ] },
    { type = "zip", path = "bin/*.zip" }
]
```

Any file paths in the configuration file may be either absolute or relative. Relative paths will be resolved relatively to the configuration file's parent directory.

The `metadata_sources` parameter (required) is a list of paths to the projects to analyze.

Currently supported metadata sources types are:
- `type = "nuget", include = "<path/to/project/or/solution>"` to extract metadata from NuGet packages used by the designated project or all projects in solution,
- `type = "license"` to provide metadata for the licenses that are not covered by NuGet packages. The `id` attribute is mandatory and should be unique across all metadata sources. The `spdx` and `copyright` attributes are mandatory. `files_covered` is also mandatory, and it should be a glob mask or a path, applied to the base directory of each declared package, to mark the files covered by the license.

  `files_covered` may be a single glob or a list of globs, applied relative to the package root of the containing package.

  `patterns_covered` (optional) is a specification of the file patterns covered by a particular source. The supported patterns are listed below, in the **Coverage Patterns Specification** section.
- `type = "reuse"` to provide licenses read according to [the REUSE specification v 3.0][reuse.spec]. Attributes:
  - `root` (required) is the root directory of the REUSE-compliant project,
  - `excludes` (optional) is a list of paths to exclude from the analysis. For example, you may want to ignore the IDE-generated files or test resources if they have different license. Any path is excluded as a subtree.
  - `files_covered` (optional) — either a glob or an array of globs. This is optional, and files that are included into the current directory and covered by the REUSE specification are _automatically_ considered as covered in any case. So, this collection should only contain the generated/built files, and not the files that are copied as-is from the REUSE-covered set of files.

    Any file that's copied as-is is considered to be covered by the exact license according to its REUSE specification. Any additional file from `files_covered` is considered to be covered by the combination of all the licenses in the source (except the `exclude`d files).
  - `patterns_covered` (optional) is a specification of the file patterns covered by a particular source. Currently supported patterns are listed below, in the **Coverage Patterns Specification** section.

  Item with `type = "reuse"` should point to a set of licenses exactly covering the sources from which the covered packaged files are built.

The `metadata_overrides` parameter (optional) should contain a set of license overrides for incorrectly marked packages in NuGet. Every record contains string fields `id`, `version`, `spdx`, and `copyright`. All fields are mandatory.

The `lock_file` parameter (optional) is the path to the license lock file that will be produced or verified by the corresponding commands. This parameter is mandatory for the `generate-lock` command.

The `packaged_files` parameter (optional) describes the list of the files you want to check for their license contents. It is a list of the entries having the following structure:
- `type` (required) should be either `directory` (to point to the root of the file hierarchy that will be processed recursively) or `zip`, to point to a zip archive that the tool will analyze,
- `path` (required) is a path on disk; for zip archives, we support glob patterns,
- `ignore` (optional) is a list of ignore specifications, see below. By default, it will be one preset named `licenses`.

The `packaged_files` parameter is mandatory for the `generate-lock` command.

### Coverage Patterns Specification
Each cover pattern specification (`patterns_covered`) is one of the following.
1. MSBuild coverage pattern:
   ```
   { type = "msbuild", include = "path/to/project.csproj/sln" }
   ```
   where
   - `type` is always `msbuild`,
   - `include` is the path to the project or solution file to cover.

   This pattern will consider the following project outputs, generated by the `Release` configuration of the project:
   - `$(TargetDir)\$(TargetFileName)`.
   - `$(TargetDir)\$(TargetName).runtimeconfig.json`.

   Additionally, it will consider files corresponding to the following globs as generated by the project, regardless of their actual contents:
   - `**/$(TargetName).pdb`,
   - `**/$(TargetName).deps.json`.
2. NuGet coverage pattern:
   ```
   { type = "nuget" }
   ```
   this pattern will consider NuGet-generated files to be covered by the license source employing this pattern:
   - `[Content_Types].xml`,
   - `_rels/.rels`,
   - `package/services/metadata/core-properties/*.psmdcp`,
   - `*.nuspec`.

### Ignore Item Specification
Each ignore item specification is a record in form of:
```
{ type = "preset", name = "licenses" }
```
where
- `type` is always `preset`,
- `name` is the name of the preset to apply.

Currently supported presets (all paths are glob patterns that are applied relatively to the package root; case-insensitive):
- `licenses`: ignores `LICENSES/*.txt`.

Lock File
---------
License lock file looks like this:
```toml
"file_name" = [
    { source_id = "FSharp.Core", source_version = "8.0.200", spdx = ["MIT"], copyright = ["© Microsoft Corporation. All rights reserved."] }
]
```
where
- `file_name` is the path of the file (or a glob) relatively to the package root.
- `source_id` _(optional)_ is the NuGet package that is the origin of the file, if it originated from a NuGet package.
- `source_version` _(optional)_ is the version of the NuGet package, if the file originates from NuGet.
- `spdx` is the list of SPDX identifiers of the license.
- `copyright` is the list of the copyright statements of the license.

One file may have several records in case it is covered by several licenses simultaneously.

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
This project's licensing follows the [REUSE specification v 3.0][reuse.spec]. Consult each file's headers and the REUSE specification for possible details.

### Contribution Policy
By contributing to this repository, you agree that any new files you contribute will be covered by the MIT license. If you want to contribute a file under a different license, you should clearly mark it in the file's header, according to the REUSE specification.

You are welcome to explicitly state your copyright in the file's header as described in [the contributor guide][docs.contributing], but the project maintainers may do this for you as well.

### Packaged Content License
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
<!-- REUSE-IgnoreEnd -->
