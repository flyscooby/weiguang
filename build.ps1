$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler was not found.' }
    New-Item -ItemType Directory -Path 'bin' -Force | Out-Null
    & $compiler /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:brightness.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /out:bin\Weiguang.exe Brightness.cs ColorControl.cs ComfortWindow.cs
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object {$_.Extension -eq '.html' -or $_.Extension -eq '.txt'} | Copy-Item -Destination 'bin' -Force
    Write-Output 'Build succeeded: bin\Weiguang.exe'
} finally { Pop-Location }
