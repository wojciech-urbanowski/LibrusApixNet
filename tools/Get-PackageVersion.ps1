#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot 'src/LibrusApixNet/LibrusApixNet.csproj'
$versionOutput = @(& dotnet msbuild $projectPath -nologo -getProperty:Version)
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the package version from MSBuild.' }
$version = ($versionOutput -join "`n").Trim()
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Invalid NuGet package version returned by MSBuild: '$version'."
}
$version
