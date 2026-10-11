#Requires -Version 7.0
[CmdletBinding()]
param([string]$PackagePath, [string]$ExpectedVersion)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $ExpectedVersion = & (Join-Path $PSScriptRoot 'Get-PackageVersion.ps1')
}
if ($ExpectedVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'Invalid expected package version.' }
if ([string]::IsNullOrWhiteSpace($PackagePath)) { throw 'Specify -PackagePath pointing to the generated .nupkg.' }
$packageFile = (Resolve-Path -LiteralPath $PackagePath).Path
$tempParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$tempRoot = Join-Path $tempParent ('LibrusApixNet-package-' + [guid]::NewGuid().ToString('N'))
$utf8 = [System.Text.UTF8Encoding]::new($false)

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Read-Entry($Archive, [string]$Name) {
    $entry = $Archive.GetEntry($Name)
    if ($null -eq $entry) { throw "Package entry missing: $Name" }
    $reader = [System.IO.StreamReader]::new($entry.Open())
    try { $reader.ReadToEnd() }
    finally { $reader.Dispose() }
}

function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE." }
}

$archive = [System.IO.Compression.ZipFile]::OpenRead($packageFile)
try {
    [xml]$manifest = Read-Entry $archive 'LibrusApixNet.nuspec'
    $metadata = $manifest.package.metadata
    Assert-Condition ($metadata.id -eq 'LibrusApixNet' -and $metadata.version -eq $ExpectedVersion) 'Unexpected package identity/version.'
    Assert-Condition ($metadata.authors -eq 'LibrusApixNet contributors') 'Unexpected package authors.'
    Assert-Condition ($metadata.license.type -eq 'expression' -and $metadata.license.InnerText -eq 'GPL-3.0-only') 'Unexpected license metadata.'
    Assert-Condition ($metadata.readme -eq 'README.md') 'Package readme is not configured.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($metadata.description) -and -not [string]::IsNullOrWhiteSpace($metadata.tags)) 'Description and tags are required.'
    Assert-Condition ($null -ne $archive.GetEntry('lib/net10.0/LibrusApixNet.dll')) 'net10.0 library is missing.'
    $dependencies = @($metadata.dependencies.group.dependency)
    Assert-Condition (@($dependencies | Where-Object { $_.id -eq 'AngleSharp' -and $_.version -eq '1.8.4' }).Count -eq 1) 'AngleSharp dependency is missing or changed.'
    Assert-Condition ((Read-Entry $archive 'LICENSE').Contains('GNU GENERAL PUBLIC LICENSE')) 'Full GPL license is missing.'
    $baseline = Get-Content (Join-Path $PSScriptRoot '../docs/upstream-map.json') -Raw | ConvertFrom-Json
    Assert-Condition ((Read-Entry $archive 'NOTICE.md').Contains($baseline.commit)) 'Upstream attribution is missing.'
    Assert-Condition ((Read-Entry $archive 'README.md').Contains('librus-apix')) 'Package readme is missing attribution.'
    Assert-Condition ($null -eq $archive.GetEntry('lib/net10.0/AngleSharp.dll')) 'AngleSharp must remain a NuGet dependency.'
    Assert-Condition ($metadata.repository.url -eq 'https://github.com/wojciech-urbanowski/LibrusApixNet.git') 'Repository URL is missing or changed.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($metadata.repository.commit)) 'Repository commit is missing.'
}
finally { $archive.Dispose() }

try {
    New-Item -ItemType Directory -Path $tempRoot | Out-Null
    $feedRoot = Join-Path $tempRoot 'local feed'
    New-Item -ItemType Directory -Path $feedRoot | Out-Null
    Copy-Item -LiteralPath $packageFile -Destination $feedRoot
    $escapedFeed = [System.Security.SecurityElement]::Escape($feedRoot)
    $configPath = Join-Path $tempRoot 'NuGet.Config'
    $config = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$escapedFeed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="local"><package pattern="LibrusApixNet" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@
    [System.IO.File]::WriteAllText($configPath, $config, $utf8)
    $projectPath = Join-Path $tempRoot 'Consumer.csproj'
    $project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <PackageReference Include="LibrusApixNet" Version="$ExpectedVersion" />
  </ItemGroup>
</Project>
"@
    [System.IO.File]::WriteAllText($projectPath, $project, $utf8)
    $program = @'
using LibrusApixNet;

var expectedVersion = args.Single();
if (LibraryInfo.Version != expectedVersion && !LibraryInfo.Version.StartsWith(expectedVersion + "+", StringComparison.Ordinal))
    throw new Exception("Unexpected library version.");
var grade = new Grade("Math", "6+", true, "2026-10-11", "", "", 1, "", "", 1);
if (grade.Value != 6.5 || new Gpa(1, "5,50", "Math").NumericValue != 5.5)
    throw new Exception("Unexpected grade values.");
var announcements = Announcements.Parse("<table class='decorated big center printable margin-top'><thead><tr><td>Test</td></tr></thead><tbody><tr class='line0'><td>Author</td></tr><tr class='line1'><td>2026-10-11</td></tr><tr class='line0'><td>Content</td></tr></tbody></table>");
if (announcements.Count != 1 || announcements[0].Title != "Test")
    throw new Exception("Parser or transitive dependency failed.");
Console.WriteLine($"Local package consumer passed: LibrusApixNet {LibraryInfo.Version}.");
'@
    [System.IO.File]::WriteAllText((Join-Path $tempRoot 'Program.cs'), $program, $utf8)
    Invoke-Dotnet @('restore', $projectPath, '--configfile', $configPath, '--packages', (Join-Path $tempRoot 'packages'), '--no-http-cache')
    Invoke-Dotnet @('run', '--project', $projectPath, '-c', 'Release', '--no-restore', '--', $ExpectedVersion)
    Write-Host 'Package metadata, contents, isolated local installation and parser execution passed.'
}
finally {
    $resolvedTarget = [System.IO.Path]::GetFullPath($tempRoot)
    if (-not $resolvedTarget.StartsWith($tempParent, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not [System.IO.Path]::GetFileName($resolvedTarget).StartsWith('LibrusApixNet-package-', [System.StringComparison]::Ordinal)) {
        throw 'Refusing to remove a temporary directory outside the expected location.'
    }
    if (Test-Path -LiteralPath $resolvedTarget) { Remove-Item -LiteralPath $resolvedTarget -Recurse -Force }
}
