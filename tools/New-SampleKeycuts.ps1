<#
.SYNOPSIS
    Writes sample keycut .bat files (and the files/folders they point to) for manual QA and long-list testing.

.DESCRIPTION
    The .bat files use the exact format that keycuts.Common's Runner.CreateShortcutFile produces, including the
    REM <description> line. The first two entries are the canonical samples (l-vicki, downloads); the rest cycle through
    URL, Folder, File and Command items with long names, long descriptions, very long destinations (200+ chars) and a few
    File items whose targets don't exist (the Missing state).

    Safety: this script only ever writes inside -Folder, and it refuses to run when -Folder
      * is C:\Shortcuts, or the output folder configured in the keycuts registry value (read only), or a parent/child of either,
      * is a drive root,
      * is outside the temp folder (unless -AllowNonTemp is given),
      * already contains .bat files that this script did not create (no .keycuts-sample marker).
    It never touches the registry or PATH.

    To point a Debug build of the app at the result without changing the registry:
        $env:KEYCUTS_OUTPUT_FOLDER = '<the folder>'

.PARAMETER Folder
    Explicit target folder. Required; there is no default.

.PARAMETER Count
    Number of keycuts to write (2 to 1000). Default 100.

.PARAMETER AllowNonTemp
    Allows a folder outside the temp folder. C:\Shortcuts and the configured output folder stay forbidden.

.EXAMPLE
    .\tools\New-SampleKeycuts.ps1 -Folder "$env:TEMP\keycuts-fixtures-100" -Count 100
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Folder,

    [ValidateRange(2, 1000)]
    [int] $Count = 100,

    [switch] $AllowNonTemp
)

$ErrorActionPreference = 'Stop'

$markerName = '.keycuts-sample'

function Get-NormalizedPath([string] $path)
{
    return [System.IO.Path]::GetFullPath($path).TrimEnd('\', '/')
}

function Test-IsSameOrChild([string] $path, [string] $parent)
{
    return $path.Equals($parent, [System.StringComparison]::OrdinalIgnoreCase) -or
        $path.StartsWith($parent + '\', [System.StringComparison]::OrdinalIgnoreCase)
}

$target = Get-NormalizedPath $Folder

if ([System.IO.Path]::GetPathRoot($target).TrimEnd('\').Equals($target, [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "Refusing to write into a drive root: $target"
}

$forbidden = @('C:\Shortcuts')
$registryFolder = (Get-ItemProperty -Path 'HKCU:\Software\TeamRalon\keycuts' -Name 'OutputFolder' -ErrorAction SilentlyContinue).OutputFolder

if ($registryFolder)
{
    $forbidden += $registryFolder
}

foreach ($path in $forbidden)
{
    $normalized = Get-NormalizedPath $path

    if ((Test-IsSameOrChild $target $normalized) -or (Test-IsSameOrChild $normalized $target))
    {
        throw "Refusing to write into or around the real keycuts output folder '$normalized': $target"
    }
}

$temp = Get-NormalizedPath ([System.IO.Path]::GetTempPath())

if (-not $AllowNonTemp -and -not $target.StartsWith($temp + '\', [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "Refusing to write outside the temp folder ($temp). Pass -AllowNonTemp to allow another explicit folder: $target"
}

$marker = Join-Path $target $markerName
$targetsFolder = Join-Path $target '_targets'

if (Test-Path -LiteralPath $target)
{
    $existingBats = @(Get-ChildItem -LiteralPath $target -Filter '*.bat' -File)

    if ($existingBats.Count -gt 0 -and -not (Test-Path -LiteralPath $marker))
    {
        throw "Refusing to write into a folder that already has .bat files not created by this script: $target"
    }

    if (Test-Path -LiteralPath $marker)
    {
        # Only a previous run of this script is replaced
        $existingBats | Remove-Item -Force

        if (Test-Path -LiteralPath $targetsFolder)
        {
            Remove-Item -LiteralPath $targetsFolder -Recurse -Force
        }
    }
}

New-Item -ItemType Directory -Path $target -Force | Out-Null
New-Item -ItemType Directory -Path $targetsFolder -Force | Out-Null
Set-Content -LiteralPath $marker -Value 'Created by tools/New-SampleKeycuts.ps1. Safe to delete.'

function Write-Keycut(
    [string] $Name,
    [string] $Type,
    [string] $Destination,
    [string] $Description,
    [string] $CommandLine)
{
    $fullPath = Join-Path $target ($Name + '.bat')

    $lines = @(
        '@ECHO OFF',
        'REM keycuts sample',
        "REM <shortcut>$fullPath</shortcut>",
        "REM <type>$Type</type>",
        "REM <destination>$Destination</destination>",
        "REM <description>$Description</description>",
        $CommandLine,
        'EXIT'
    )

    [System.IO.File]::WriteAllLines($fullPath, $lines)
}

function Write-Url([string] $Name, [string] $Url, [string] $Description)
{
    # Same escaping as Shortcut.SanitizeBatEscapeCharacters
    $escaped = [regex]::Replace($Url, '(?<!%)%(?!%)', '%%')
    $escaped = [regex]::Replace($escaped, '(?<!^)&(?!&)', '^&')

    Write-Keycut $Name 'url' $Url $Description "START $escaped"
}

function Write-Folder([string] $Name, [string] $Destination, [string] $Description)
{
    Write-Keycut $Name 'folder' $Destination $Description ('"%SystemRoot%\explorer.exe" "{0}"' -f $Destination)
}

function Write-File([string] $Name, [string] $Destination, [string] $Description)
{
    Write-Keycut $Name 'file' $Destination $Description ('START "" /B "{0}"' -f $Destination)
}

function Write-Command([string] $Name, [string] $CommandText, [string] $Description)
{
    $commandLine = 'START "" /B "%windir%\System32\cmd.exe" /k ' + $CommandText

    Write-Keycut $Name 'command' ('"%windir%\System32\cmd.exe" /k ' + $CommandText) $Description $commandLine
}

function New-ExistingFile([string] $Name)
{
    $path = Join-Path $targetsFolder $Name
    Set-Content -LiteralPath $path -Value 'sample'

    return $path
}

function New-ExistingFolder([string] $Name)
{
    $path = Join-Path $targetsFolder $Name
    New-Item -ItemType Directory -Path $path -Force | Out-Null

    return $path
}

Write-Url 'l-vicki' 'https://logs.example.com/vicki' 'logs for vicki'
Write-Folder 'downloads' '%USERPROFILE%\Downloads' 'Downloads folder'

$longQuery = 'q=' + ('abcdefghij' * 22)
$longPathSegment = 'very-long-folder-name-' + ('x' * 30)

for ($i = 2; $i -lt $Count; $i++)
{
    $number = $i + 1
    $name = 'sample-{0:000}' -f $number
    $description = "Sample keycut $number"

    switch (($i - 2) % 12)
    {
        0 { Write-Url $name "https://example.com/items/$number" $description }
        1 { Write-Folder $name (New-ExistingFolder "folder-$number") $description }
        2 { Write-File $name (New-ExistingFile "file-$number.txt") $description }
        3 { Write-Command $name "echo hello $number" $description }
        4
        {
            $longName = "a-very-long-keycut-name-for-layout-testing-$number"
            $longDescription = "A deliberately long description for keycut $number that should be truncated or wrapped instead of breaking the row layout " + ('lorem ipsum ' * 8)
            Write-Folder $longName (New-ExistingFolder "folder-$number") $longDescription.Trim()
        }
        5 { Write-Url $name "https://example.com/search?$longQuery&page=$number&sort=desc" $description }
        6 { Write-File $name (New-ExistingFile "file-$number.txt") $description }
        7 { Write-Folder $name (New-ExistingFolder "folder-$number") $description }
        8
        {
            # Target intentionally missing, with a destination over 200 characters
            $missing = Join-Path $targetsFolder ("missing\$longPathSegment\$longPathSegment\$longPathSegment\$longPathSegment\file-$number.txt")
            Write-File $name $missing $description
        }
        9 { Write-Url $name "https://example.com/docs/$number" $description }
        10
        {
            Write-Command $name "echo long $number" ('Runs a command with a long description that goes on and on ' * 3).Trim()
        }
        11 { Write-File $name (New-ExistingFile "file-$number.txt") $description }
    }
}

Write-Output $target
