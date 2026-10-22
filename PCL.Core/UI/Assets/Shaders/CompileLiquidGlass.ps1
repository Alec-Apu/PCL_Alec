$ErrorActionPreference = 'Stop'
$compiler = Get-Command fxc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
if (-not $compiler) {
    $compiler = Get-ChildItem "${env:ProgramFiles(x86)}/Windows Kits/10/bin/*/x64/fxc.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -ExpandProperty FullName -First 1
}
if (-not $compiler) { throw 'Install the Windows SDK shader compiler (fxc.exe).' }
& $compiler /T ps_3_0 /E PixelShaderFunction /O3 /Fo "$PSScriptRoot/LiquidGlass.ps" "$PSScriptRoot/LiquidGlass.hlsl"
if ($LASTEXITCODE -ne 0) { throw "LiquidGlass compilation failed ($LASTEXITCODE)." }
