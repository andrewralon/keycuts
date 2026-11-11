<#
.SYNOPSIS
    Builds the per-user keycuts MSI at artifacts\installer\<runtime>\keycuts-setup-<arch>.msi.

.DESCRIPTION
    Publishes the GUI and CLI self-contained into artifacts\publish\<runtime>, then packages that folder. The CLI's
    keycuts.cmd and keypaste.cmd shims go in its bin subfolder, the only folder of it the MSI puts on PATH. Self-contained because a non-admin install can't add the .NET runtime to the machine.
    Needs only the .NET SDK; the WiX toolset restores from NuGet. Each runtime builds into its own folders, so building
    one doesn't delete the other.

.PARAMETER Version
    Overrides the version for this build only. Without it, the build uses <Version> from Directory.Build.props,
    which is what releases use.

.PARAMETER Runtime
    win-x64 (the default) or win-arm64. Both MSIs share an upgrade code, so installing one replaces the other.
#>
param(
    [string]$Version,
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$arch = $Runtime -replace '^win-', ''
$name = "keycuts-setup-$arch"
$publish = Join-Path $root "artifacts\publish\$Runtime"
$output = Join-Path $root "artifacts\installer\$Runtime"

foreach ($folder in $publish, $output) {
    if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
}

$versionArgs = if ($Version) { @("-p:Version=$Version") } else { @() }

foreach ($project in 'keycuts.GUI', 'keycuts.CLI') {
    dotnet publish (Join-Path $root "$project\$project.csproj") -c Release -r $Runtime --self-contained true `
        -p:SatelliteResourceLanguages=en @versionArgs -o $publish
    if ($LASTEXITCODE -ne 0) { throw "Publishing $project for $Runtime failed." }
}

# --no-incremental: both runtimes share the project's obj folder, and an incremental build after the other runtime's
# skips linking and then can't find this runtime's MSI
dotnet build (Join-Path $root 'installer\keycuts.Installer.wixproj') -c Release --no-incremental `
    @versionArgs -p:PublishDir=$publish -p:InstallerPlatform=$arch -p:OutputName=$name -o $output
if ($LASTEXITCODE -ne 0) { throw "Building the $arch MSI failed." }

# A project with a .wxl file builds into a folder per culture
$culture = Join-Path $output 'en-US'
Move-Item (Join-Path $culture '*') $output
Remove-Item $culture

Get-Item (Join-Path $output "$name.msi") | Select-Object FullName, @{ n = 'SizeMB'; e = { [math]::Round($_.Length / 1MB, 1) } }
