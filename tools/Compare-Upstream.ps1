#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$SourcePath,
    [switch]$ShowDiff,
    [switch]$FailOnChanges,
    [switch]$PassThru
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    throw 'Podaj -SourcePath wskazujący lokalny katalog librus-apix / Specify -SourcePath pointing to a local librus-apix checkout.'
}
$projectRoot = Split-Path -Parent $PSScriptRoot
$baseline = Get-Content -LiteralPath (Join-Path $projectRoot 'docs/upstream-map.json') -Raw | ConvertFrom-Json
$sourceRoot = (Resolve-Path -LiteralPath $SourcePath).Path
if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot 'librus_apix'))) {
    throw "Brak katalogu librus_apix w $sourceRoot"
}

function Get-NormalizedHash([string]$Path) {
    $content = [System.IO.File]::ReadAllText($Path).Replace("`r`n", "`n")
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $algorithm.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($content))
        return ([System.BitConverter]::ToString($hash)).Replace('-', '').ToLowerInvariant()
    }
    finally { $algorithm.Dispose() }
}

$tracked = @{}
$changes = @()
foreach ($entry in @($baseline.modules) + @($baseline.tests)) {
    $tracked[$entry.source] = $true
    $path = Join-Path $sourceRoot $entry.source
    if (-not (Test-Path -LiteralPath $path)) { $status = 'DELETED' }
    elseif ((Get-NormalizedHash $path) -ne $entry.sha256) { $status = 'CHANGED' }
    else { continue }
    $changes += [pscustomobject]@{ Status = $status; Source = $entry.source; Target = $entry.target; Symbols = $entry.symbols }
}
foreach ($folder in @('librus_apix', 'tests')) {
    $path = Join-Path $sourceRoot $folder
    if (-not (Test-Path -LiteralPath $path)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $path -Filter '*.py' -File -Recurse) {
        if ($file.FullName -match '[\\/]__pycache__[\\/]') { continue }
        $relative = [System.IO.Path]::GetRelativePath($sourceRoot, $file.FullName).Replace('\', '/')
        if (-not $tracked.ContainsKey($relative)) {
            $changes += [pscustomobject]@{ Status = 'ADDED'; Source = $relative; Target = 'UNMAPPED'; Symbols = @() }
        }
    }
}

if ($PassThru) { $changes }
else {
    Write-Host "Baseline: $($baseline.commit)"
    Write-Host "Źródło: $sourceRoot"
    if ($changes.Count -eq 0) { Write-Host 'Brak zmian w modułach i testach źródłowych.' }
    else { $changes | Format-Table Status, Source, Target -AutoSize -Wrap }
}

if ($ShowDiff -and $changes.Count -gt 0) {
    & git -C $sourceRoot diff $baseline.commit -- ($changes | ForEach-Object { $_.Source })
    if ($LASTEXITCODE -ne 0) { throw 'Nie można odczytać diffu względem commita bazowego.' }
    Write-Host 'Nowe pliki nieśledzone przez Git są pokazane w tabeli; git diff nie wyświetla ich treści.'
}
if ($FailOnChanges -and $changes.Count -gt 0) { exit 1 }
