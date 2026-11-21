<#
.SYNOPSIS
  Renders a hand-written HTML mockup to a PNG with headless Edge or Chrome (Chrome on macOS).

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
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
    # macOS: Chrome first; headless Edge there took the shot before the page painted
    '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
    '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge'
)
$browser = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $browser) { throw 'Neither Edge nor Chrome was found.' }

$url = [System.Uri]::new($htmlPath).AbsoluteUri
if ($IsMacOS) {
    & $browser --headless=new --disable-gpu --hide-scrollbars --virtual-time-budget=2000 "--window-size=$Width,$Height" "--screenshot=$outPath" $url 2>$null | Out-Null
} else {
    & $browser --headless --disable-gpu --hide-scrollbars "--window-size=$Width,$Height" "--screenshot=$outPath" $url | Out-Null
}

if (-not (Test-Path -LiteralPath $outPath)) { throw "The browser wrote no image to $outPath." }
Write-Host "Wrote $outPath"
