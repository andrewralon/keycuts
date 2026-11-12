<#
.SYNOPSIS
    Regenerates the README's screenshots in docs/images.

.DESCRIPTION
    Runs keycuts.Tests' ScreenshotTests, which shows the app's real views off screen with sample keycuts and writes each
    one to a PNG, in the light and the dark palette: the creator, the creator with a name warning, the manager, the Edit
    form and Settings (creator-light.png, creator-dark.png, and so on).

    The views are rendered at twice their size, without the window frame, so the result doesn't depend on your theme,
    display or what is open, and it never touches your keycuts folder, the registry or PATH. The dates read "3 days ago"
    and so on from now, so running it again changes a file only when a view changed.

    Do this when preparing a release (see "Versioning and releases" in CLAUDE.md), and whenever a change moves how a
    screenshotted view looks. Look at the images, then commit what changed. It builds in Release, so it works while a
    Debug copy of the app is open.

.PARAMETER Folder
    Where to write the PNGs. Default: docs/images in this repository.

.EXAMPLE
    pwsh -File tools/New-Screenshots.ps1
#>
[CmdletBinding()]
param(
    [string] $Folder
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not $Folder)
{
    $Folder = Join-Path $repoRoot 'docs\images'
}

$output = [System.IO.Path]::GetFullPath($Folder)
New-Item -ItemType Directory -Force -Path $output | Out-Null

# Without the variable the test does nothing, so a normal test run never writes anywhere
$env:KEYCUTS_SCREENSHOTS_OUT = $output

try
{
    & dotnet test (Join-Path $repoRoot 'keycuts.Tests\keycuts.Tests.csproj') -c Release --filter 'FullyQualifiedName~ScreenshotTests'

    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet test failed with exit code $LASTEXITCODE."
    }
}
finally
{
    Remove-Item Env:\KEYCUTS_SCREENSHOTS_OUT -ErrorAction SilentlyContinue
}

Write-Host "Screenshots written to $output :"
Get-ChildItem -Path $output -Filter '*.png' | Sort-Object Name | ForEach-Object { Write-Host ('  {0}  {1:N0} KB' -f $_.Name, ($_.Length / 1KB)) }
