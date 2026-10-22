$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$resultPath = Join-Path $PSScriptRoot 'test-results'
New-Item -ItemType Directory -Path $resultPath -Force | Out-Null
$testExe = Join-Path $PSScriptRoot 'dist\IrisClipboardNext.exe'
$testProcess = Start-Process -FilePath $testExe -ArgumentList @('--self-test', ('"' + $resultPath + '"')) -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath (Join-Path $resultPath 'self-test.txt')
if ($testProcess.ExitCode -ne 0) { throw 'Storage tests failed.' }
$uiProcess = Start-Process -FilePath $testExe -ArgumentList @('--ui-smoke', ('"' + $resultPath + '"')) -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath (Join-Path $resultPath 'ui-smoke.txt')
if ($uiProcess.ExitCode -ne 0) { throw 'UI smoke test failed.' }
$startupProcess = Start-Process -FilePath $testExe -ArgumentList @('--startup-smoke', ('"' + $resultPath + '"')) -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath (Join-Path $resultPath 'startup-smoke.txt')
if ($startupProcess.ExitCode -ne 0) { throw 'Startup smoke test failed.' }
$phaseProcess = Start-Process -FilePath $testExe -ArgumentList @('--phase2-test', ('"' + $resultPath + '"')) -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath (Join-Path $resultPath 'phase2-tests.txt')
if ($phaseProcess.ExitCode -ne 0) { throw 'Phase two tests failed.' }

$captureProcess = Start-Process -FilePath $testExe -ArgumentList @('--capture-test', ('"' + $resultPath + '"')) -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath (Join-Path $resultPath 'capture-tests.txt')
if ($captureProcess.ExitCode -ne 0) { throw 'Capture mode tests failed.' }
$languageProcess = Start-Process -FilePath $testExe -ArgumentList @('--language-test', ('"' + $resultPath + '"')) -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath (Join-Path $resultPath 'language-tests.txt')
if ($languageProcess.ExitCode -ne 0) { throw 'Language tests failed.' }