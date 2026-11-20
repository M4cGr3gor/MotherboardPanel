# Compila Motherboard Panel y deja el .exe en esta carpeta.
#   .\build.ps1         compila
#   .\build.ps1 -Test   además compila y ejecuta las pruebas
param([switch]$Test)

$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
$out = Join-Path $src 'build'
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
$core = @("$src\logic.cpp")
$coreSystem = @("$src\system.cpp")
$libs = @('user32.lib', 'shell32.lib', 'advapi32.lib', 'ole32.lib', 'oleaut32.lib', 'taskschd.lib', 'wtsapi32.lib', 'userenv.lib', 'comsuppw.lib')

function Invoke-Native([string]$what, [scriptblock]$cmd) {
    & $cmd
    if ($LASTEXITCODE -ne 0) { throw "$what falló (código $LASTEXITCODE)" }
}

$present = { param($files) @($files | Where-Object { Test-Path $_ }) }

if ($Test) {
    $files = @($core) + (& $present $coreSystem)
    Invoke-Native 'Compilar pruebas' { cl @cflags "/Fo$out\\" "/Fe$out\tests.exe" @files "$src\tests\tests.cpp" /link @libs | Out-Host }
    Invoke-Native 'Pruebas' { & "$out\tests.exe" | Out-Host }
}

if (Test-Path "$src\main.cpp") {
    Push-Location $src
    try { Invoke-Native 'Compilar recursos' { rc /nologo /fo "$out\app.res" "$src\app.rc" | Out-Host } }
    finally { Pop-Location }
    Invoke-Native 'Compilar Motherboard Panel' {
        cl @cflags "/Fo$out\\" "/Fe$out\Motherboard Panel.exe" "$src\main.cpp" @core @coreSystem "$out\app.res" `
            /link /SUBSYSTEM:WINDOWS /MANIFEST:EMBED "/MANIFESTINPUT:$src\app.manifest" @libs | Out-Host
    }
    Copy-Item "$out\Motherboard Panel.exe" (Join-Path $src 'Motherboard Panel.exe') -Force
}
Write-Host 'Compilación terminada.' -ForegroundColor Green
