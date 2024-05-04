# SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
#
# SPDX-License-Identifier: MIT

param (
    [Parameter(Mandatory = $true)]
    [string] $NewVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function replacePattern($regex, $fileName) {
    $content = Get-Content -LiteralPath $fileName -Raw
    $newContent = $content -replace $regex, ('${1}' + $NewVersion + '${2}')
    if ($content -eq $newContent) {
        throw "No changes were made in file `"$fileName`": regex `"$regex`" did not match anything?"
    }
    [IO.File]::WriteAllText($fileName, $newContent)
}

replacePattern '(<Version>).*?(</Version>)' 'Directory.Build.props'
replacePattern '(DotNetLicenses/bin/Release/FVNever\.DotNetLicenses\.).*?(\.nupkg)' '.dotnet-licenses.toml'
