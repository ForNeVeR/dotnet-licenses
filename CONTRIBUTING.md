<!--
SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>

SPDX-License-Identifier: MIT
-->

Contributor Guide
=================

Prerequisites
-------------
To work with the project, you'll need [.NET SDK 8][dotnet-sdk] or later.

Build
-----
Use the following shell command:

```console
$ dotnet build
```

Pack
----
To prepare a NuGet package with the tool, use the following shell command:

```console
$ dotnet pack
```

GitHub Actions
--------------
If you want to update the GitHub Actions used in the project, edit the file that generated them: `scripts/github-actions.fsx`.

Then run the following shell command:
```console
$ dotnet fsi scripts/github-actions.fsx
```

File Encoding Changes
---------------------
If the automation asks you to update the file encoding (line endings or UTF-8 BOM) in certain files, run the following PowerShell script ([PowerShell Core][powershell] is recommended to run this script):
```console
$ pwsh -File scripts/Test-Encoding.ps1 -AutoFix
```

The `-AutoFix` switch will automatically fix the encoding issues, and you'll only need to commit and push the changes.

Copyright Year Updates
----------------------
If the automation asks you to update the copyright years in certain files, either fix it manually, or install [REUSE][reuse] and then try the following shell command:
```console
$ pwsh -File scripts/Test-LicenseHeaders.ps1 -AutoFix
```

[dotnet-sdk]: https://dotnet.microsoft.com/en-us/download
[powershell]: https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell
[reuse]: https://reuse.software/
