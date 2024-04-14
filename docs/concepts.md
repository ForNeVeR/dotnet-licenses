<!--
SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>

SPDX-License-Identifier: MIT
-->

Core Concepts
=============
There are a number of core concepts used by dotnet-license. This document describes them in detail.

Metadata Sources
----------------
Metadata sources are the places where the tool looks for the information about the licenses. Each metadata source contains information about licenses and copyright statements, and about files these licenses are applied to.

`dotnet-licenses` relies on the configured metadata sources to provide the information on the user files. The sources we support are:

- NuGet packages: `dotnet-licenses` should automatically determine the files that belong to the installed NuGet packages, and use the licenses the packages are distributed under.
- Explicit licenses: the user is able to provide the licensing information for certain files manually.

It is also possible to override information from the sources, for cases when they are incorrect.

License Lock File
-----------------
One of the main purposes of `dotnet-licenses` is to generate the license lock file that stores the information about packaged files and their licensing requirements. It is supposed that this file may be shipped with the package, and it should regenerate on any changes to the project's licensing requirements.

When a file is no longer licensed by a particular source, this information should be removed from the lock file, to prevent it from accumulating stale items.

Packaged Files
--------------
The main input to the tool is a set of files that are part of some distribution package. The tool checks all the files and tries to find their licenses among the configured metadata sources.

Output Data
-----------
There are several ways to distribute the resulting information within the package. For now, we are targeting the result to be [REUSE][reuse]-compatible, perhaps by embedding a `.reuse/dep5` file into the package (and the `LICENSES` directory).

[reuse]: https://reuse.software/
