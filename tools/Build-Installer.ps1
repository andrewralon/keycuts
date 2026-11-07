<#
.SYNOPSIS
    Builds the per-user keycuts MSI at artifacts\installer\keycuts-setup.msi.

.DESCRIPTION
    Publishes the GUI, CLI and keypaste self-contained into one folder, then packages that folder.
    Self-contained because a non-admin install can't add the .NET runtime to the machine.
    Needs only the .NET SDK; the WiX toolset restores from NuGet.

.PARAMETER Version
    MSI version, three parts. Raise it for each release so upgrades replace the previous install.
#>
param(
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $root 'artifacts\publish'
$output = Join-Path $root 'artifacts\installer'

foreach ($folder in $publish, $output) {
    if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
}

foreach ($project in 'keycuts.GUI', 'keycuts.CLI', 'keypaste') {
    dotnet publish (Join-Path $root "$project\$project.csproj") -c Release -r win-x64 --self-contained true `
        -p:SatelliteResourceLanguages=en -o $publish
    if ($LASTEXITCODE -ne 0) { throw "Publishing $project failed." }
}

dotnet build (Join-Path $root 'installer\keycuts.Installer.wixproj') -c Release `
    -p:ProductVersion=$Version -p:PublishDir=$publish -o $output
if ($LASTEXITCODE -ne 0) { throw 'Building the MSI failed.' }

# A project with a .wxl file builds into a folder per culture
$culture = Join-Path $output 'en-US'
Move-Item (Join-Path $culture '*') $output
Remove-Item $culture

Get-Item (Join-Path $output 'keycuts-setup.msi') | Select-Object FullName, @{ n = 'SizeMB'; e = { [math]::Round($_.Length / 1MB, 1) } }
