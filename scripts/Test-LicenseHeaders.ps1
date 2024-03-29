# SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
#
# SPDX-License-Identifier: MIT

<#
    .SYNOPSIS
        Checks that all files in the repository have the correct license headers. A correct header should have a
        right set of copyright years.
#>
param (
    # Path to the repository root.
    $SourceRoot = "$PSScriptRoot/..",

    # Makes the script to perform file modifications to update the copyright years correctly.
    [switch] $Autofix
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$errors = @()

function getLastCommitYear($file) {
    Write-Host "Getting the last commit year for the file $file."
    $year = git log -1 --format=%ad --date=format:%Y -- $file
    if (!$?) {
        throw "Failed to get the last commit year for the file $file. Exit code from git: $LASTEXITCODE."
    }
    if ($year -eq $null){
        return $null
    } else {
        return [int]::Parse($year, [Globalization.CultureInfo]::InvariantCulture)
    }
}

function getCopyrightYear($copyright) {
    if ($copyright.value -match '^SPDX-FileCopyrightText:\s+(?:\d{4}\s*-\s*)?(\d{4})' -or
        $copyright.value -match '^(?:\d{4}\s*-\s*)?(\d{4})') {
        [int]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture)
    } else {
        $null
    }
}

function getLastCopyrightYear($copyrights) {
    $years = $copyrights | ForEach-Object {
        getCopyrightYear $_
    }
    return $years | Sort-Object -Descending | Select-Object -First 1
}

function applyCopyrightYearFix($file, $year) {
    if ($file.spdx_expressions.Length -gt 1) {
        throw "More than 1 SPDX expression in the file $($file.path), please update manually."
    }

    $existingCopyright = $file.copyrights | Sort-Object -Property { getCopyrightYear $_ } | Select-Object -Last 1
    $license = $file.spdx_expressions[0].value
    if ($existingCopyright.value -match '^(?:SPDX-FileCopyrightText:)?\s*(?:(?:\d{4}\s*-\s*)?\d{4})\s+(.*)') {
        $copyright = $Matches[1]
        reuse annotate --copyright $copyright --license $license --year $year --merge-copyrights $file.path
        if (!$?) {
            throw "Failed to update the copyright in the file $($file.path). Exit code from reuse: $LASTEXITCODE."
        }
    } else {
        throw "Cannot parse the copyright $($existingCopyright.value) in the file $($file.path)."
    }
}

Push-Location $SourceRoot
try {
    $data = reuse lint -j | ConvertFrom-Json
    foreach ($file in $data.files) {
        $lastCommitYear = getLastCommitYear $file.path
        if ($lastCommitYear -eq $null) {
            continue
        }
        $lastCopyrightYear = getLastCopyrightYear $file.copyrights
        if ($lastCommitYear -gt $lastCopyrightYear) {
            if ($Autofix) {
                Write-Host "Error found in file $($file.path). Attempting to autofix."
                applyCopyrightYearFix $file $lastCommitYear
            } else {
                Write-Host "Error found in file $($file.path)."
                [array] $errors += [PSCustomObject] @{
                    File = $file.path
                    ExpectedYear = $lastCommitYear
                    ExistingYear = $lastCopyrightYear
                }
            }
        }
    }

    if ($errors.Length) {
        $errorMessage = "$($errors.Length) errors in copyrights:`n" + ($errors | ForEach-Object {
            "File: $($_.File), last change year: $($_.ExpectedYear), copyright year: $($_.ExistingYear)`n"
        } | Out-String)
        throw $errorMessage
    } else {
        Write-Host 'No errors found.'
    }
} finally {
    Pop-Location
}
