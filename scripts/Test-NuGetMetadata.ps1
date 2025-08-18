# SPDX-FileCopyrightText: 2024-2025 Friedrich von Never <friedrich@fornever.me>
#
# SPDX-License-Identifier: MIT

param (
    # Path to the repository root.
    $SourceRoot = "$PSScriptRoot/..",

    # Makes the script to perform file modifications to update the metadata.
    [switch] $Autofix
)

# REUSE-IgnoreStart
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$directoryBuildProps = [xml]::new()
$directoryBuildProps.PreserveWhitespace = $true
$directoryBuildProps.Load($(Resolve-Path -LiteralPath "$SourceRoot/Directory.Build.props"))
$packagingGroup = $directoryBuildProps.Project.PropertyGroup `
    | Where-Object { $_.Label -eq 'Packaging' } `
    | Select-Object -First 1

$packageLicenseExpression = $packagingGroup.PackageLicenseExpression
$copyrightStatements = $packagingGroup.Copyright

# TODO[#74]: This should be performed using the tooling provided by dotnet-authors in the future.
function normalizeCopyrights($copyrights) {
    $normalized = @()
    foreach ($copyright in $copyrights) {
        if ($copyright -eq '© 2024 Friedrich von Never') {
            $normalized += '2024 Friedrich von Never <friedrich@fornever.me>'
        } else {
            $normalized += $copyright
        }
    }
    return $normalized
}

function readLockFile($path) {
    Add-Type -LiteralPath "$SourceRoot/DotNetLicenses/bin/Release/net9.0/Tomlyn.dll"
    $content = Get-Content -Raw -LiteralPath $path
    $toml = [Tomlyn.Toml]::ToModel($content)

    $allLicenses = @()
    $allCopyrights = @()

    foreach ($item in $toml.Values) {
        $spdx = $null
        $copyright = $null
        $null = $item.TryGetValue('spdx', [ref]$spdx)
        $null = $item.TryGetValue('copyright', [ref]$copyright)

        if ($spdx -ne $null) {
            $allLicenses += $spdx
        }
        if ($copyright -ne $null) {
            $allCopyrights += $copyright
        }
    }

    $allCopyrights = normalizeCopyrights $allCopyrights

    [array] $allLicenses = $allLicenses | Select-Object -Unique
    [array] $allCopyrights = $allCopyrights | Select-Object -Unique

    [pscustomobject] @{
        Licenses = $allLicenses
        Copyrights = $allCopyrights
    }
}

$lockFileContents = readLockFile "$SourceRoot/.dotnet-licenses.lock.toml"
$expectedLicenseExpression = $lockFileContents.Licenses -join ' AND '
$expectedCopyright = $lockFileContents.Copyrights -join "`n"

if ($Autofix) {
    $directoryBuildProps.PreserveWhitespace = $true
    $packagingGroup.PackageLicenseExpression = $expectedLicenseExpression
    $packagingGroup.Copyright = $expectedCopyright
    $directoryBuildProps.Save($(Resolve-Path -LiteralPath "$SourceRoot/Directory.Build.props"))
} else {
    $diagnostics = ''
    if ($packageLicenseExpression -ne $expectedLicenseExpression) {
        $diagnostics += "Expected license expression: $expectedLicenseExpression`n"
        $diagnostics += "Actual license expression: $packageLicenseExpression`n"
    }
    if ($copyrightStatements -ne $expectedCopyright) {
        $diagnostics += "Expected copyright statement: $expectedCopyright`n"
        $diagnostics += "Actual copyright statement: $copyrightStatements`n"
    }
    if ($diagnostics -ne '') {
        Write-Error $diagnostics
    }
}
# REUSE-IgnoreEnd
