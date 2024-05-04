<!--
SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>

SPDX-License-Identifier: MIT
-->

Core Concepts
=============
Essentially, dotnet-licenses is a tool that analyzes the package content, finds its origins and the licensing information linked to this origin, and generates a license lock file that can be used to distribute the licensing information with the package.

To do all that, it needs several pieces of information about your software, that are described in this document.

Metadata Sources
----------------
Metadata sources are the places where the tool looks for the information about the licenses. Each metadata source contains information about licenses and copyright statements, and about files these licenses are applied to.

dotnet-licenses relies on the configured metadata sources to provide the information on the user files. The sources we support are:

- NuGet packages: dotnet-licenses should automatically determine the files that belong to the installed NuGet packages, and use the licenses the packages are distributed under.

  It is also possible to override information from the sources, for cases when the packages are incorrect or not consistent (e.g. doesn't provide SPDX identifier when required).
- Explicit licenses: the user is able to provide the licensing information for certain files manually.
- REUSE-compliant packages: the tool should be able to read the information according to the REUSE specification, and use the licensing information from there.

License Lock File
-----------------
One of the main purposes of dotnet-licenses is to generate the license lock file that stores the information about packaged files and their licensing requirements. It is supposed that this file may be shipped with the package, and it should regenerate on any changes to the project's licensing requirements.

When a file is no longer licensed by a particular source, this information should be removed from the lock file, to prevent it from accumulating stale items.

Packaged Files
--------------
The main input to the tool is a set of files that are part of some distribution package. The tool checks all the files and tries to find their licenses among the configured metadata sources.

[reuse]: https://reuse.software/
