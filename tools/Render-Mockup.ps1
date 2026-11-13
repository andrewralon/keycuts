<#
.SYNOPSIS
  Renders a hand-written HTML mockup to a PNG with headless Edge (or Chrome).

.DESCRIPTION
  For design sketches the owner approves before anything is built (see docs/mockups).
  The HTML is a picture of an idea, not the real WPF app, so don't present it as a screenshot.
  Nothing is downloaded: it uses the Edge or Chrome already on the machine.

.EXAMPLE
  pwsh -File tools/Render-Mockup.ps1 -Html docs/mockups/creator-manager-v2.html
  pwsh -File tools/Render-Mockup.ps1 -Html docs/mockups/creator-manager-v2.html -Out C:\tmp\v2.png -Width 1400 -Height 600
#>
param(
    [Parameter(Mandatory = $true)][string]$Html,
    [string]$Out,
    [int]$Width = 1370,
    [int]$Height = 560
)

$ErrorActionPreference = 'Stop'

$htmlPath = (Resolve-Path -LiteralPath $Html).Path
if (-not $Out) { $Out = [System.IO.Path]::ChangeExtension($htmlPath, '.png') }
$outPath = [System.IO.Path]::GetFullPath($Out)

$candidates = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
)
$browser = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $browser) { throw 'Neither Edge nor Chrome was found.' }

$url = ([System.Uri]$htmlPath).AbsoluteUri
& $browser --headless --disable-gpu --hide-scrollbars "--window-size=$Width,$Height" "--screenshot=$outPath" $url | Out-Null

if (-not (Test-Path -LiteralPath $outPath)) { throw "The browser wrote no image to $outPath." }
Write-Host "Wrote $outPath"
