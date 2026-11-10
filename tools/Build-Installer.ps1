<#
.SYNOPSIS
    Builds the per-user keycuts MSI at artifacts\installer\keycuts-setup.msi.

.DESCRIPTION
    Publishes the GUI and CLI (with its keypaste.cmd shim) self-contained into one folder, then packages that folder.
    Self-contained because a non-admin install can't add the .NET runtime to the machine.
    Needs only the .NET SDK; the WiX toolset restores from NuGet.

.PARAMETER Version
    Overrides the version for this build only. Without it, the build uses <Version> from Directory.Build.props,
    which is what releases use.
#>
param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $root 'artifacts\publish'
$output = Join-Path $root 'artifacts\installer'

foreach ($folder in $publish, $output) {
    if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
}

$versionArgs = if ($Version) { @("-p:Version=$Version") } else { @() }

foreach ($project in 'keycuts.GUI', 'keycuts.CLI') {
    dotnet publish (Join-Path $root "$project\$project.csproj") -c Release -r win-x64 --self-contained true `
        -p:SatelliteResourceLanguages=en @versionArgs -o $publish
    if ($LASTEXITCODE -ne 0) { throw "Publishing $project failed." }
}

dotnet build (Join-Path $root 'installer\keycuts.Installer.wixproj') -c Release `
    @versionArgs -p:PublishDir=$publish -o $output
if ($LASTEXITCODE -ne 0) { throw 'Building the MSI failed.' }

# A project with a .wxl file builds into a folder per culture
$culture = Join-Path $output 'en-US'
Move-Item (Join-Path $culture '*') $output
Remove-Item $culture

Get-Item (Join-Path $output 'keycuts-setup.msi') | Select-Object FullName, @{ n = 'SizeMB'; e = { [math]::Round($_.Length / 1MB, 1) } }
