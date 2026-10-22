$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'Windows .NET Framework 4.x compiler is required.' }
$outputPath = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
& $compilerPath /nologo /target:exe /r:System.Drawing.dll /r:System.Windows.Forms.dll "/out:$outputPath\IconMaker.exe" (Join-Path $projectRoot 'tools\IconMaker.cs') (Join-Path $projectRoot 'src\Theme.cs') (Join-Path $projectRoot 'src\Language.cs')
if ($LASTEXITCODE -ne 0) { throw 'Icon builder failed.' }
& (Join-Path $outputPath 'IconMaker.exe') (Join-Path $outputPath 'ClipboardNext.ico')
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
& $compilerPath /nologo /target:winexe /platform:x64 /optimize+ /utf8output /codepage:65001 /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Runtime.Serialization.dll "/win32icon:$outputPath\ClipboardNext.ico" "/out:$outputPath\ClipboardNext.exe" $sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built: $outputPath\ClipboardNext.exe"
