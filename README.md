<!--
SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>

SPDX-License-Identifier: MIT
-->

dotnet-licenses
===============
dotnet-licenses is a set of tooling to maintain the software license information in published packages.

The general approach is inspired by [REUSE][reuse], but adopted for binary packages.

Different software licenses have different requirements, but most of them require you to bring the copyright information together with any form of distribution.

dotnet-licenses will help you to verify that every file in your package is covered by a license, and wil help the package consumers to determine exactly what file is covered by what.

Documentation
-------------
- [Contributor Guide][docs.contributing]

License
-------
This project's licensing follows the [REUSE specification v 3.0][reuse.spec]. The main license for the source is MIT, consult each file's headers and the REUSE specification for possible details.

[docs.contributing]: CONTRIBUTING.md
[reuse.spec]: https://reuse.software/spec/
[reuse]: https://reuse.software/
