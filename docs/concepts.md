<!--
SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>

SPDX-License-Identifier: MIT
-->

Core Concepts
=============
There are a number of core concepts used by dotnet-license. This document describes them in detail.

Metadata Sources
----------------
Metadata sources are the places where the tool looks for the information about the licenses. `dotnet-licenses` relies on the metadata sources to provide the information on the user files. The sources we aim to support are:

- NuGet packages: `dotnet-licenses` should automatically determine the files that belong to the installed NuGet packages, and use the licenses the packages are distributed under.
- Custom metadata: the user is able to provide the licensing information for certain files manually.

Each metadata source is supposed to provide a mapping between files and the licenses the files are distributed under.

The tool configuration should also be able to provide the metadata overrides for possibly incorrect information provided by the main sources.

License Lock File
-----------------
The tool is supposed to generate the license lock file: it stores the information about packaged files and their licensing requirements. It is supposed that this file may be shipped with the package, and it should regenerate on any changes to the project's licensing requirements.

When a license comes away from the package, we should also remove it from the lock file, to prevent stale items from appearing.

Packaged Files
--------------
The main input to the tool is a set of files that are part of some distribution package. The tool checks all the files and tries to find their licenses in the metadata sources.

Output Data
-----------
There are several ways to distribute the resulting information within the package. For now, we are targeting the result to be [REUSE][reuse]-compatible, perhaps by embedding a `.reuse/dep5` file into the package (and the `LICENSES` directory).

[reuse]: https://reuse.software/
