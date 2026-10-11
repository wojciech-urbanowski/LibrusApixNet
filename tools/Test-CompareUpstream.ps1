#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$tempParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$tempRoot = Join-Path $tempParent ('LibrusApixNet-comparison-' + [guid]::NewGuid().ToString('N'))
$pwshPath = (Get-Process -Id $PID).Path
$utf8 = [System.Text.UTF8Encoding]::new($false)

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Invoke-ComparisonProcess([string[]]$Arguments) {
    $output = & $pwshPath -NoProfile -NonInteractive -File $comparePath @Arguments 2>&1 | Out-String
    [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output }
}

function Invoke-Git([string[]]$Arguments) {
    $output = & git -C $sourceRoot @Arguments 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Git failed: $output" }
    $output.Trim()
}

try {
    $toolsRoot = Join-Path $tempRoot 'tools'
    $docsRoot = Join-Path $tempRoot 'docs'
    $sourceRoot = Join-Path $tempRoot 'source with spaces'
    foreach ($directory in @($toolsRoot, $docsRoot, (Join-Path $sourceRoot 'librus_apix'), (Join-Path $sourceRoot 'tests'))) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    $comparePath = Join-Path $toolsRoot 'Compare-Upstream.ps1'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Compare-Upstream.ps1') -Destination $comparePath

    $text = "# baseline`nvalue = 1`n"
    $hash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($utf8.GetBytes($text))).ToLowerInvariant()
    $modulePath = Join-Path $sourceRoot 'librus_apix/sample.py'
    $deletedPath = Join-Path $sourceRoot 'librus_apix/deleted.py'
    $testPath = Join-Path $sourceRoot 'tests/test_sample.py'
    foreach ($path in @($modulePath, $deletedPath, $testPath)) {
        [System.IO.File]::WriteAllText($path, $text, $utf8)
    }
    Invoke-Git @('init', '--quiet') | Out-Null
    Invoke-Git @('add', '.') | Out-Null
    Invoke-Git @('-c', 'user.name=Portability test', '-c', 'user.email=test@example.invalid', '-c', 'commit.gpgsign=false', 'commit', '--quiet', '-m', 'Fixture baseline') | Out-Null
    $commit = Invoke-Git @('rev-parse', 'HEAD')
    $baseline = @{
        schemaVersion = 1
        commit = $commit
        modules = @(
            @{ source = 'librus_apix/sample.py'; target = 'Sample.cs'; symbols = @{}; sha256 = $hash },
            @{ source = 'librus_apix/deleted.py'; target = 'Deleted.cs'; symbols = @{}; sha256 = $hash }
        )
        tests = @(@{ source = 'tests/test_sample.py'; target = 'SampleTests.cs'; sha256 = $hash })
    }
    [System.IO.File]::WriteAllText((Join-Path $docsRoot 'upstream-map.json'), ($baseline | ConvertTo-Json -Depth 8), $utf8)

    $missing = Invoke-ComparisonProcess @()
    Assert-Condition ($missing.ExitCode -ne 0 -and $missing.Output.Contains('-SourcePath')) 'Missing SourcePath must fail without prompting.'
    $invalid = Invoke-ComparisonProcess @('-SourcePath', $tempRoot)
    Assert-Condition ($invalid.ExitCode -ne 0) 'An invalid upstream directory must fail.'
    $clean = Invoke-ComparisonProcess @('-SourcePath', $sourceRoot, '-FailOnChanges')
    Assert-Condition ($clean.ExitCode -eq 0) "Clean baseline failed: $($clean.Output)"
    Assert-Condition (@(& $comparePath -SourcePath $sourceRoot -PassThru).Count -eq 0) 'Clean PassThru must return no changes.'

    [System.IO.File]::WriteAllText($modulePath, $text.Replace("`n", "`r`n"), $utf8)
    $lineEndings = Invoke-ComparisonProcess @('-SourcePath', $sourceRoot, '-FailOnChanges')
    Assert-Condition ($lineEndings.ExitCode -eq 0) 'CRLF and LF must have the same baseline hash.'

    [System.IO.File]::WriteAllText($modulePath, "# changed`nvalue = 2`n", $utf8)
    Remove-Item -LiteralPath $deletedPath
    [System.IO.File]::WriteAllText((Join-Path $sourceRoot 'librus_apix/new.py'), "# added`n", $utf8)
    $cacheRoot = Join-Path $sourceRoot 'librus_apix/__pycache__'
    New-Item -ItemType Directory -Path $cacheRoot | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $cacheRoot 'ignored.py'), '# ignored', $utf8)
    $changes = @(& $comparePath -SourcePath $sourceRoot -PassThru)
    Assert-Condition ($changes.Count -eq 3) "Expected exactly three changes, got $($changes.Count)."
    foreach ($expected in @(
        @{ Status = 'CHANGED'; Source = 'librus_apix/sample.py' },
        @{ Status = 'DELETED'; Source = 'librus_apix/deleted.py' },
        @{ Status = 'ADDED'; Source = 'librus_apix/new.py' }
    )) {
        Assert-Condition (@($changes | Where-Object { $_.Status -eq $expected.Status -and $_.Source -eq $expected.Source }).Count -eq 1) "Missing $($expected.Status): $($expected.Source)"
    }
    Assert-Condition (($changes | Where-Object Status -eq 'ADDED').Target -eq 'UNMAPPED') 'Added files must be unmapped.'
    $changed = Invoke-ComparisonProcess @('-SourcePath', $sourceRoot, '-FailOnChanges')
    Assert-Condition ($changed.ExitCode -eq 1) 'FailOnChanges must exit with code 1.'
    $normal = Invoke-ComparisonProcess @('-SourcePath', $sourceRoot)
    Assert-Condition ($normal.ExitCode -eq 0) 'Changes without FailOnChanges must remain a successful comparison.'
    $diff = Invoke-ComparisonProcess @('-SourcePath', $sourceRoot, '-ShowDiff')
    Assert-Condition ($diff.ExitCode -eq 0 -and $diff.Output.Contains('diff --git')) "ShowDiff failed: $($diff.Output)"
    Write-Host 'Upstream comparison passed: paths with spaces, validation, LF/CRLF, changed/added/deleted files, PassThru, exit codes and Git diff.'
}
finally {
    $resolvedTarget = [System.IO.Path]::GetFullPath($tempRoot)
    if (-not $resolvedTarget.StartsWith($tempParent, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not [System.IO.Path]::GetFileName($resolvedTarget).StartsWith('LibrusApixNet-comparison-', [System.StringComparison]::Ordinal)) {
        throw 'Refusing to remove a temporary directory outside the expected location.'
    }
    if (Test-Path -LiteralPath $resolvedTarget) { Remove-Item -LiteralPath $resolvedTarget -Recurse -Force }
}
