$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
Push-Location $projectRoot
try {
    New-Item -ItemType Directory -Path 'test-results' -Force | Out-Null
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /target:exe /main:UIReview /win32icon:brightness.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /out:test-results\UIReview.exe tests\UIReview.cs Brightness.cs ColorControl.cs ComfortWindow.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & '.\test-results\UIReview.exe' (Join-Path $projectRoot 'test-results\ui-preview.png')
    if ($LASTEXITCODE -ne 0) { throw 'UI checks failed.' }
} finally { Pop-Location }
