$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

# Run build.ps1 and test.ps1 before packaging. This script only packages a built app.
$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$workspaceRoot = [IO.Directory]::GetParent($projectRoot).FullName
$releaseName = 'clipboard-0.4.0-win-x64'
$releaseRoot = Join-Path $workspaceRoot 'releases'
$packageRoot = Join-Path $releaseRoot $releaseName
$archivePath = Join-Path $releaseRoot ($releaseName + '.zip')
$appName = 'clipboard历史剪切板.exe'
$guideName = '使用说明.txt'
$checksumName = 'SHA256.txt'
$sources = @(
    @{ Source = (Join-Path $projectRoot 'dist\IrisClipboardNext.exe'); Name = $appName },
    @{ Source = (Join-Path $projectRoot 'dist\IrisClipboardNext.ico'); Name = 'clipboard.ico' },
    @{ Source = (Join-Path $projectRoot ('docs\' + $guideName)); Name = $guideName },
    @{ Source = (Join-Path $projectRoot 'docs\User-Guide-English.txt'); Name = 'User-Guide-English.txt' }
)

foreach ($item in $sources) {
    if (-not (Test-Path -LiteralPath $item.Source -PathType Leaf)) {
        throw ('Required release input is missing: ' + $item.Source)
    }
}

# Repeated runs may replace only these known release files. Never collect user data.
$allowedNames = @($sources | ForEach-Object { $_.Name }) + @($checksumName)
if (Test-Path -LiteralPath $packageRoot) {
    if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
        throw ('Release output is not a folder: ' + $packageRoot)
    }
    $unexpected = @(Get-ChildItem -LiteralPath $packageRoot -Force | Where-Object { $_.PSIsContainer -or $_.Name -notin $allowedNames })
    if ($unexpected.Count -gt 0) {
        throw ('Release folder contains unexpected files. Keep them elsewhere before packaging: ' + $packageRoot)
    }
}

$releaseExe = Join-Path $packageRoot $appName
$running = @(Get-Process | Where-Object {
    try { $_.Path -and [string]::Equals($_.Path, $releaseExe, [StringComparison]::OrdinalIgnoreCase) } catch { $false }
})
if ($running.Count -gt 0) {
    throw 'The packaged app is running. Exit it from the system tray, then package again.'
}

# Detect a locked output before changing any existing package files.
$outputs = @($allowedNames | ForEach-Object { Join-Path $packageRoot $_ }) + @($archivePath)
foreach ($path in $outputs) {
    if (Test-Path -LiteralPath $path) {
        $stream = $null
        try { $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
        catch { throw ('Release output is in use or not writable: ' + $path + '. Exit the app and close the archive before retrying.') }
        finally { if ($null -ne $stream) { $stream.Dispose() } }
    }
}

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
foreach ($item in $sources) {
    Copy-Item -LiteralPath $item.Source -Destination (Join-Path $packageRoot $item.Name) -Force
}

$checksumLines = @('SHA-256 / clipboard 0.4.0 / Windows x64', '')
foreach ($item in $sources) {
    $destination = Join-Path $packageRoot $item.Name
    $sourceHash = (Get-FileHash -LiteralPath $item.Source -Algorithm SHA256).Hash
    $copyHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    if ($sourceHash -ne $copyHash) { throw ('Copied file verification failed: ' + $destination) }
    $checksumLines += ($copyHash + '  ' + $item.Name)
}
[IO.File]::WriteAllLines((Join-Path $packageRoot $checksumName), $checksumLines, (New-Object Text.UTF8Encoding($true)))

$packageFiles = @($allowedNames | ForEach-Object { Join-Path $packageRoot $_ })
Compress-Archive -LiteralPath $packageFiles -DestinationPath $archivePath -CompressionLevel Optimal -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $entryNames = @($archive.Entries | ForEach-Object { $_.FullName })
    if ($entryNames.Count -ne $allowedNames.Count -or @(Compare-Object $allowedNames $entryNames).Count -ne 0) {
        throw 'Archive content verification failed.'
    }
    foreach ($entry in $archive.Entries) {
        $entryStream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $entryHash = [BitConverter]::ToString($sha.ComputeHash($entryStream)).Replace('-', '') }
        finally { $sha.Dispose(); $entryStream.Dispose() }
        $fileHash = (Get-FileHash -LiteralPath (Join-Path $packageRoot $entry.FullName) -Algorithm SHA256).Hash
        if ($entryHash -ne $fileHash) { throw ('Archive checksum verification failed: ' + $entry.FullName) }
    }
} finally { $archive.Dispose() }

Write-Output ('Release folder: ' + $packageRoot)
Write-Output ('Release archive: ' + $archivePath)
Write-Output ('Archive SHA256: ' + (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash)
