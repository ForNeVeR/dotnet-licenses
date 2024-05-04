# SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
#
# SPDX-License-Identifier: MIT

param (
    # Path to the repository root.
    $SourceRoot = "$PSScriptRoot/..",

    # Makes the script to perform file modifications to update the metadata.
    [switch] $Autofix
)

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

function readLockFile($path) {
    @{
        Licenses = @('MIT', 'Apache-2.0')
        Copyrights = @('2024 Friedrich von Never <friedrich@fornever.me>', '2024 Friedrich von Never <friedrich@fornever.me>')
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
