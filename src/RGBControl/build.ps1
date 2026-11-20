# Compila RGB Control (C#) y RGB Helper (C++), y deja los dos .exe en esta carpeta.
#   .\build.ps1         compila
#   .\build.ps1 -Test   además ejecuta las pruebas de C++ y de C#
param([switch]$Test)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$src = Join-Path $root 'src\RgbHelper'
$out = Join-Path $root 'build\helper'
New-Item -ItemType Directory -Force $out | Out-Null

# Carga las variables de entorno del compilador de Visual C++ (vcvars64) en esta sesión.
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'No se encontró el compilador de Visual C++ (Visual Studio Build Tools).' }
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'
cmd /c "`"$vcvars`" >nul 2>&1 && set" | ForEach-Object {
    if ($_ -match '^([^=]+)=(.*)$') { [Environment]::SetEnvironmentVariable($matches[1], $matches[2]) }
}

$cflags = @('/nologo', '/std:c++20', '/O2', '/MT', '/EHsc', '/W4', '/utf-8', '/DUNICODE', '/D_UNICODE', '/DNOMINMAX', '/DWIN32_LEAN_AND_MEAN')
$core = @("$src\config.cpp", "$src\effects.cpp", "$src\frame.cpp", "$src\presence.cpp")

function Invoke-Native([string]$what, [scriptblock]$cmd) {
    & $cmd
    if ($LASTEXITCODE -ne 0) { throw "$what falló (código $LASTEXITCODE)" }
}

if ($Test) {
    Invoke-Native 'Compilar pruebas C++' { cl @cflags "/Fo$out\\" "/Fe$out\tests.exe" @core "$src\tests\tests.cpp" /link advapi32.lib | Out-Host }
    Invoke-Native 'Pruebas C++' { & "$out\tests.exe" | Out-Host }
}

if (Test-Path "$src\main.cpp") {
    # rc resuelve la ruta del icono (..\RgbApp\app.ico) desde la carpeta actual.
    Push-Location $src
    try { Invoke-Native 'Compilar recursos' { rc /nologo /fo "$out\app.res" "$src\app.rc" | Out-Host } }
    finally { Pop-Location }
    Invoke-Native 'Compilar RGB Helper' {
        cl @cflags "/Fo$out\\" "/Fe$out\RGB Helper.exe" "$src\main.cpp" "$src\hid_device.cpp" @core "$out\app.res" `
            /link /SUBSYSTEM:WINDOWS /MANIFEST:EMBED "/MANIFESTINPUT:$src\app.manifest" `
            user32.lib shell32.lib gdi32.lib hid.lib setupapi.lib advapi32.lib | Out-Host
    }
    # (Copia para Motherboard Panel: no se cierra el ayudante instalado; el .exe se instala con el script del paquete.)
    Copy-Item "$out\RGB Helper.exe" (Join-Path $root 'RGB Helper.exe') -Force
}

# vcvars64 define Platform=x64, y con ella dotnet compilaría en bin\x64\... en lugar de bin\Release\net48,
# dejando sin actualizar el .exe que se copia abajo.
Remove-Item Env:Platform -ErrorAction SilentlyContinue

if ($Test) {
    Invoke-Native 'Pruebas C#' { dotnet test (Join-Path $root 'src\RgbCore.Tests') -nologo -v q | Out-Host }
}
Invoke-Native 'Compilar RGB Control' { dotnet build (Join-Path $root 'src\RgbApp') -c Release -nologo -v q | Out-Host }
Copy-Item (Join-Path $root 'src\RgbApp\bin\Release\net48\RGB Control.exe') (Join-Path $root 'RGB Control.exe') -Force
Write-Host 'Compilación terminada.' -ForegroundColor Green
