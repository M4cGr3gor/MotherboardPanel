<#
.SYNOPSIS
    Instala Motherboard Panel con la pantalla USB (Turing System Monitor, con sus correcciones) y las luces
    RGB (RGB Control) en una misma carpeta, o lo desinstala.

.DESCRIPTION
    Contenido del paquete (junto a este script):
        Motherboard Panel\Motherboard Panel.exe
        Pantalla\Turing system monitor 3.10.0.exe   instalador oficial de la pantalla
        Pantalla\Programa corregido\                archivos que cambian respecto a la 3.10.0 oficial (4 parches)
        Pantalla\Parches\                           los parches y su LEEME.txt
        Pantalla\Temas\<tema>\                      temas propios (y sus generadores)
        Luces\RGB Control.exe, Luces\RGB Helper.exe
        Drivers\PawnIO_<versión>.exe                driver PawnIO
        Drivers\LibreHardwareMonitor_<versión>\     LibreHardwareMonitor (se instala dentro de la pantalla)

    Instalación (carpeta elegible; por defecto C:\Program Files\Motherboard panel):
        <carpeta>\Motherboard Panel.exe
        <carpeta>\Pantalla\    pantalla oficial 3.10.0 + correcciones + temas + LibreHardwareMonitor
        <carpeta>\Luces\       RGB Control y RGB Helper

    Sin parámetros muestra un menú. También admite:  -Accion Instalar | Desinstalar | Estado
#>
param(
    [ValidateSet('Menu', 'Instalar', 'Desinstalar', 'Estado')]
    [string]$Accion = 'Menu',

    # Uso interno: SID del usuario que lanzó el script, para conservarlo al elevar permisos
    [string]$UsuarioSid
)

$ErrorActionPreference = 'Stop'

$Paquete        = $PSScriptRoot
$PkgPanel       = Join-Path $Paquete 'Motherboard Panel'
$PkgPantalla    = Join-Path $Paquete 'Pantalla'
$PkgLuces       = Join-Path $Paquete 'Luces'
$PkgDrivers     = Join-Path $Paquete 'Drivers'
$CarpetaDefecto = Join-Path $env:ProgramFiles 'Motherboard panel'
$ClaveRegistro  = 'HKLM:\SOFTWARE\Motherboard Panel'
$Tarea          = 'Motherboard Panel'
$TemaPreferido  = 'BloquesColorPestanas'
$VersionPawnIO  = [version]'2.2.0.0'
$VersionLhm     = '0.9.6'
$ClaveDesinst   = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MotherboardPanel'
$NombreDesinst  = 'Motherboard Panel - pantalla USB y luces RGB'
$ScriptDesinst  = 'Desinstalar.ps1'   # copia de este script en la carpeta instalada
$ContenidoPropio = 'Motherboard Panel.exe', 'Motherboard Panel.ini', 'Pantalla', 'Luces', $ScriptDesinst   # lo que puede haber en la carpeta
$ClaveTuring    = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{5D52BFDE-9A40-4BFA-84F8-89A64B01A2AC}_is1'

# --- Usuario y permisos --------------------------------------------------------

if (-not $UsuarioSid) { $UsuarioSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value }
$UsuarioNombre = (New-Object Security.Principal.SecurityIdentifier $UsuarioSid).Translate([Security.Principal.NTAccount]).Value

function Test-Admin {
    $p = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if ($Accion -ne 'Estado' -and -not (Test-Admin)) {
    $argumentos = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Accion $Accion -UsuarioSid $UsuarioSid"
    try { Start-Process powershell.exe -Verb RunAs -ArgumentList $argumentos }
    catch { Write-Host 'Se necesitan permisos de administrador y no se han concedido.' -ForegroundColor Red; Read-Host 'Pulsa Intro para salir' }
    exit
}

# --- Utilidades ----------------------------------------------------------------

function Copy-Carpeta([string]$Origen, [string]$Destino, [string[]]$Excluir = @(), [string[]]$ExcluirCarpetas = @()) {
    $opciones = @($Origen, $Destino, '/E', '/R:2', '/W:1', '/NFL', '/NDL', '/NJH', '/NJS', '/NP')
    if ($Excluir) { $opciones += '/XF'; $opciones += $Excluir }
    if ($ExcluirCarpetas) { $opciones += '/XD'; $opciones += $ExcluirCarpetas }
    & robocopy @opciones | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "No se ha podido copiar '$Origen' (robocopy, código $LASTEXITCODE). ¿Hay algún archivo abierto?" }
}

function Get-VersionTexto([string]$Carpeta) {
    $f = Join-Path $Carpeta 'version.txt'
    if (Test-Path -LiteralPath $f) { (Get-Content -LiteralPath $f -TotalCount 1).Trim() } else { $null }
}

function Get-TamanoMB([string]$Carpeta) {
    (Get-ChildItem -LiteralPath $Carpeta -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum / 1MB
}

function Get-VersionArchivo($Archivo) {
    try { [version]$Archivo.VersionInfo.ProductVersion } catch { [version]'0.0' }
}

Add-Type -Namespace MBP -Name Win -MemberDefinition @'
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
public static extern IntPtr FindWindow(string clase, string titulo);
[DllImport("user32.dll")]
public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
'@ -ErrorAction SilentlyContinue

# Procesos cuyo ejecutable está en la carpeta indicada.
function Get-ProcesosEn([string]$Carpeta, [string]$Nombre) {
    @(Get-CimInstance Win32_Process -Filter "Name='$Nombre'" | Where-Object { $_.ExecutablePath -and (Split-Path $_.ExecutablePath) -eq $Carpeta.TrimEnd('\') })
}

function Wait-Fin($Procesos, [int]$Segundos) {
    foreach ($p in @($Procesos)) { Wait-Process -Id $p.ProcessId -Timeout $Segundos -ErrorAction SilentlyContinue }
}

function Set-Evento([string]$Nombre) {
    try { $ev = [Threading.EventWaitHandle]::OpenExisting($Nombre); $ev.Set() | Out-Null; $ev.Dispose(); $true } catch { $false }
}

# --- Carpeta de instalación ----------------------------------------------------

function Get-CarpetaInstalada {
    $c = (Get-ItemProperty $ClaveRegistro -ErrorAction SilentlyContinue).InstallDir
    if ($c -and (Test-Path -LiteralPath (Join-Path $c 'Motherboard Panel.exe'))) { return $c.TrimEnd('\') }
    $null
}

# Motherboard Panel y la pantalla se ejecutan con permisos de administrador: solo los administradores deben
# poder cambiar sus archivos. Archivos de programa ya está protegida; otra carpeta (p. ej. D:\Programas) podría
# heredar permisos de escritura para cualquier usuario, así que se le quitan los heredados.
function Protect-Carpeta([string]$Carpeta) {
    $protegidas = @($env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { $_ } | ForEach-Object { $_.TrimEnd('\') + '\' }
    if ($protegidas | Where-Object { ($Carpeta.TrimEnd('\') + '\').StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }) { return }
    New-Item -ItemType Directory -Path $Carpeta -Force | Out-Null
    # Administradores y SYSTEM: control total; Usuarios: leer y ejecutar.
    & icacls.exe $Carpeta /inheritance:r /grant:r '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-545:(OI)(CI)RX' /Q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "No se han podido ajustar los permisos de $Carpeta (icacls, código $LASTEXITCODE)." }
    Write-Host "Permisos de $Carpeta ajustados: solo los administradores pueden modificarla." -ForegroundColor Green
}

function Read-Carpeta {
    $actual = Get-CarpetaInstalada
    $defecto = if ($actual) { $actual } else { $CarpetaDefecto }
    $r = (Read-Host "Carpeta de instalación (Intro = $defecto)").Trim().Trim('"')
    if (-not $r) { $r = $defecto }
    if (-not [IO.Path]::IsPathRooted($r)) { throw "Indica una ruta completa, por ejemplo $CarpetaDefecto" }
    $r = $r.TrimEnd('\')
    # Al desinstalar se borra la carpeta entera: no puede tener nada más que Motherboard Panel (completo o a medias).
    $ajenos = @(Get-ChildItem -LiteralPath $r -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -notin $ContenidoPropio })
    if ($ajenos) {
        throw "La carpeta $r ya contiene otros archivos ($(($ajenos | Select-Object -First 3).Name -join ', ')...). Elige una carpeta nueva o vacía: al desinstalar se borra entera."
    }
    $r
}

# --- Detener los programas -----------------------------------------------------

function Stop-Todo([string]$Carpeta) {
    # Motherboard Panel primero (sin "Salir": así no reinicia nada mientras se instala).
    Get-ProcesosEn $Carpeta 'Motherboard Panel.exe' | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
    # Pantalla: cierre limpio (apaga la pantalla). [NullString]::Value: PowerShell convertiría $null en "".
    $pantalla = Join-Path $Carpeta 'Pantalla'
    $main = Get-ProcesosEn $pantalla 'main.exe'
    if ($main) {
        $h = [MBP.Win]::FindWindow('turingEventWndClass', [NullString]::Value)
        if ($h -ne [IntPtr]::Zero) { [void][MBP.Win]::PostMessage($h, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) }
        Wait-Fin $main 10
    }
    foreach ($n in 'main.exe', 'configure.exe', 'theme-editor.exe') { Get-ProcesosEn $pantalla $n | ForEach-Object { Stop-Process -Id $_.ProcessId -Force } }
    # Luces: el ayudante se cierra devolviendo las luces a su efecto normal.
    $luces = Join-Path $Carpeta 'Luces'
    $helper = Get-ProcesosEn $luces 'RGB Helper.exe'
    if ($helper) { [void](Set-Evento 'RGBControl.Stop'); Wait-Fin $helper 5 }
    foreach ($n in 'RGB Helper.exe', 'RGB Control.exe') { Get-ProcesosEn $luces $n | ForEach-Object { Stop-Process -Id $_.ProcessId -Force } }
}

# --- Pantalla ------------------------------------------------------------------

function Install-PantallaOficial([string]$Destino) {
    $instalador = Get-ChildItem -LiteralPath $PkgPantalla -Filter 'Turing system monitor*.exe' | Select-Object -First 1
    if (-not $instalador) { throw "No está el instalador oficial de la pantalla en $PkgPantalla" }
    Write-Host "Instalando la pantalla oficial en $Destino (modo silencioso)..."
    # /NOICONS: sin carpeta "Turing System Monitor" en el menú Inicio. /COMPONENTS sin "themes": no copia los
    # temas oficiales (solo se usan los propios).
    $p = Start-Process -FilePath $instalador.FullName -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/NOICONS', '/COMPONENTS=program,config', "/DIR=`"$Destino`"" -Wait -PassThru
    if (-not (Test-Path -LiteralPath (Join-Path $Destino 'main.exe'))) { throw "El instalador oficial ha terminado (código $($p.ExitCode)) pero la pantalla no está en $Destino." }
    Remove-RastrosTuring
    Write-Host "Pantalla oficial $(Get-VersionTexto $Destino) instalada." -ForegroundColor Green
}

# La pantalla forma parte de Motherboard Panel: ni carpeta propia en el menú Inicio ni entrada propia en
# "Aplicaciones instaladas" (se desinstala con Motherboard Panel).
function Remove-RastrosTuring {
    foreach ($menu in [Environment]::GetFolderPath('Programs'), [Environment]::GetFolderPath('CommonPrograms')) {
        $grupo = Join-Path $menu 'Turing System Monitor'
        if (Test-Path -LiteralPath $grupo) { Remove-Item -LiteralPath $grupo -Recurse -Force }
    }
    $claves = "HKCU:\$ClaveTuring", "Registry::HKEY_USERS\$UsuarioSid\$ClaveTuring", "HKLM:\$ClaveTuring",
              ('HKLM:\Software\WOW6432Node\' + $ClaveTuring.Substring('Software\'.Length))
    foreach ($clave in $claves) { if (Test-Path -LiteralPath $clave) { Remove-Item -LiteralPath $clave -Recurse -Force } }
}

function Install-Correcciones([string]$Destino) {
    $origen = Join-Path $PkgPantalla 'Programa corregido'
    $nueva = Get-VersionTexto $origen
    # Las correcciones traen retoques de cada tema oficial: no se copian (solo se usan los temas propios).
    Copy-Carpeta $origen $Destino -ExcluirCarpetas @(Get-TemasOficiales | ForEach-Object FullName)
    if ((Get-VersionTexto $Destino) -ne $nueva) { throw "Tras copiar las correcciones, la versión de la pantalla no es '$nueva'." }
    Write-Host "Correcciones instaladas (versión $nueva)." -ForegroundColor Green
}

# Temas oficiales: los que retoca el programa corregido (las mismas carpetas que trae la pantalla oficial).
function Get-TemasOficiales {
    Get-ChildItem -LiteralPath (Join-Path $PkgPantalla 'Programa corregido\res\themes') -Directory -ErrorAction SilentlyContinue
}

# Fuentes de la pantalla oficial que no usan los temas propios (chinas, japonesas y pixeladas: 170 MB).
# Se conservan las latinas, útiles para crear temas nuevos, y NotoColorEmoji, que usa el configurador.
$FuentesSobrantes = 'BoutiqueBitmap9x9', 'Cubic_11', 'GlowSansSC-Compressed', 'SourceHanSansCN', 'fusion-pixel', 'misaki'
# Imágenes de res\docs que usa el programa: configurador (tema sin vista previa) y editor de temas (tema con errores).
$DocsUsados = 'no-preview.png', 'error-in-theme.png'

# Quita lo que la pantalla oficial deja y Motherboard Panel no usa: temas oficiales (de una instalación
# anterior), fuentes sobrantes y los archivos de Python 3.13 del instalador oficial que el programa corregido
# (Python 3.14) ha sustituido. Los temas y fuentes que añada el usuario se conservan.
function Remove-Sobrantes([string]$Destino) {
    $antes = Get-TamanoMB $Destino
    foreach ($t in Get-TemasOficiales) {
        $c = Join-Path $Destino "res\themes\$($t.Name)"
        if (Test-Path -LiteralPath $c) { Remove-Item -LiteralPath $c -Recurse -Force }
    }
    foreach ($f in $FuentesSobrantes) {
        $c = Join-Path $Destino "res\fonts\$f"
        if (Test-Path -LiteralPath $c) { Remove-Item -LiteralPath $c -Recurse -Force }
    }
    # res\docs son fotos para la documentación en GitHub; el programa solo usa estas dos.
    Get-ChildItem -LiteralPath (Join-Path $Destino 'res\docs') -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notin $DocsUsados } | Remove-Item -Force
    Get-ChildItem -LiteralPath $Destino -Recurse -File | Where-Object { $_.Name -like '*.cp313-*' -or $_.Name -eq 'python313.dll' } | Remove-Item -Force
    # La librería de cálculo de numpy para 3.13 (la de 3.14 viene en el programa corregido; msvcp140-* la usan las dos).
    $blas = Join-Path $PkgPantalla 'Programa corregido\numpy.libs'
    Get-ChildItem -LiteralPath (Join-Path $Destino 'numpy.libs') -Filter 'libscipy_openblas*' -ErrorAction SilentlyContinue |
        Where-Object { -not (Test-Path -LiteralPath (Join-Path $blas $_.Name)) } | Remove-Item -Force
    Write-Host ("Quitados temas oficiales, fuentes sobrantes, fotos de la documentación y restos de Python 3.13: {0:N0} MB menos." -f ($antes - (Get-TamanoMB $Destino))) -ForegroundColor Green
}

function Get-TemasPropios {
    $c = Join-Path $PkgPantalla 'Temas'
    if (Test-Path -LiteralPath $c) { Get-ChildItem -LiteralPath $c -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'theme.yaml') } | Sort-Object Name }
}

function Install-Temas([string]$Destino) {
    foreach ($t in @(Get-TemasPropios)) {
        Copy-Carpeta $t.FullName (Join-Path $Destino "res\themes\$($t.Name)")
        Write-Host "Tema $($t.Name) instalado." -ForegroundColor Green
    }
}

function Select-TemaActivo {
    $temas = @(Get-TemasPropios)
    if ($temas.Count -le 1) { return ($temas | Select-Object -First 1 -ExpandProperty Name) }
    $defecto = if ($temas.Name -contains $TemaPreferido) { $TemaPreferido } else { $temas[0].Name }
    Write-Host 'Temas propios disponibles:'
    for ($i = 0; $i -lt $temas.Count; $i++) { Write-Host "  $($i + 1)) $($temas[$i].Name)$(if ($temas[$i].Name -eq $defecto) { '  (por defecto)' })" }
    $r = (Read-Host "¿Qué tema activar? (1-$($temas.Count), Intro = $defecto)").Trim()
    if ($r -match '^\d+$' -and [int]$r -ge 1 -and [int]$r -le $temas.Count) { return $temas[[int]$r - 1].Name }
    $defecto
}

function Set-ValorConfig([string[]]$Lineas, [string]$Clave, [string]$Valor) {
    $patron = "^(\s*$Clave\s*:\s*)('[^']*'|`"[^`"]*`"|[^#\s]*)(.*)$"
    $hecho = $false
    $r = foreach ($l in $Lineas) { if (-not $hecho -and $l -match $patron) { $hecho = $true; "$($Matches[1])$Valor$($Matches[3])" } else { $l } }
    if (-not $hecho) { throw "No se ha encontrado la opción $Clave en config.yaml" }
    , $r
}

function Set-Configuracion([string]$Destino) {
    $config = Join-Path $Destino 'config.yaml'
    $tema = Select-TemaActivo
    $eth = Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq 'Up' -and $_.MediaType -eq '802.3' } |
        Sort-Object ifIndex | Select-Object -First 1 -ExpandProperty Name
    $lineas = [IO.File]::ReadAllLines($config)
    if ($tema) { $lineas = Set-ValorConfig $lineas 'THEME' $tema }
    if ($eth) { $lineas = Set-ValorConfig $lineas 'ETH' "'$eth'" }
    [IO.File]::WriteAllLines($config, $lineas, (New-Object Text.UTF8Encoding $false))
    Write-Host "Configuración: tema '$tema'$(if ($eth) { ", red '$eth'" })." -ForegroundColor Green
}

# --- Drivers -------------------------------------------------------------------

function Get-PaqueteLhm {
    Get-ChildItem -LiteralPath $PkgDrivers -Directory -Filter "LibreHardwareMonitor_$VersionLhm" | Select-Object -First 1
}

# 'correcto' si los archivos instalados son idénticos a los del paquete; si no, la versión encontrada o 'no instalado'.
function Test-Lhm([string]$Pantalla) {
    $destino = Join-Path $Pantalla 'external\LibreHardwareMonitor'
    $dll = Join-Path $destino 'LibreHardwareMonitorLib.dll'
    if (-not (Test-Path -LiteralPath $dll)) { return 'no instalado' }
    $pkg = Get-PaqueteLhm
    $distintos = @(Get-ChildItem -LiteralPath $pkg.FullName -File | Where-Object {
        $d = Join-Path $destino $_.Name
        -not (Test-Path -LiteralPath $d) -or (Get-FileHash -LiteralPath $d -Algorithm MD5).Hash -ne (Get-FileHash -LiteralPath $_.FullName -Algorithm MD5).Hash
    })
    if ($distintos.Count -eq 0) { return 'correcto' }
    "versión $((Get-Item -LiteralPath $dll).VersionInfo.ProductVersion.Split('+')[0]), $($distintos.Count) archivo(s) distinto(s)"
}

function Install-Lhm([string]$Pantalla) {
    $pkg = Get-PaqueteLhm
    if (-not $pkg) { throw "No está LibreHardwareMonitor $VersionLhm en $PkgDrivers" }
    $estado = Test-Lhm $Pantalla
    if ($estado -ne 'correcto') {
        Write-Host "LibreHardwareMonitor: $estado -> se instala la $VersionLhm."
        Copy-Carpeta $pkg.FullName (Join-Path $Pantalla 'external\LibreHardwareMonitor')
        $estado = Test-Lhm $Pantalla
        if ($estado -ne 'correcto') { throw "LibreHardwareMonitor no ha quedado correcto: $estado" }
    }
    Write-Host "LibreHardwareMonitor $VersionLhm correcto." -ForegroundColor Green
}

# Versión de PawnIO correctamente instalada (entrada de desinstalación + servicio + driver), o $null.
function Get-VersionPawnIO {
    $clave = Get-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO' -ErrorAction SilentlyContinue
    $servicio = Get-Service PawnIO -ErrorAction SilentlyContinue
    $driver = (Get-CimInstance Win32_SystemDriver -Filter "Name='PawnIO'" -ErrorAction SilentlyContinue).PathName
    if (-not ($clave -and $servicio -and $driver)) { return $null }
    $sys = $driver -replace '^\\SystemRoot', $env:SystemRoot -replace '^\\\?\?\\', ''
    if (-not (Test-Path -LiteralPath $sys)) { return $null }
    try { [version]$clave.DisplayVersion } catch { [version]'0.0' }
}

# PawnIO borrado pero todavía cargado: Windows no deja volver a instalarlo hasta reiniciar (error 1072).
function Test-PawnIOPendiente {
    (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\PawnIO' -ErrorAction SilentlyContinue).DeleteFlag -eq 1
}
$AvisoReinicio = 'El driver PawnIO está pendiente de eliminarse y Windows no deja instalarlo de nuevo hasta reiniciar. Reinicia Windows y vuelve a ejecutar el instalador.'

function Get-InstaladorPawnIO {
    $inst = Get-ChildItem -LiteralPath $PkgDrivers -File -Filter 'PawnIO*.exe' | Where-Object { (Get-VersionArchivo $_) -eq $VersionPawnIO } | Select-Object -First 1
    if (-not $inst) { throw "No está el instalador de PawnIO $VersionPawnIO en $PkgDrivers" }
    $firma = Get-AuthenticodeSignature -LiteralPath $inst.FullName
    if ($firma.Status -ne 'Valid') { throw "La firma digital de $($inst.Name) no es válida ($($firma.Status))." }
    $inst
}

# Quita el PawnIO instalado con el desinstalador de la 2.2.0, que también sabe quitar el driver instalado
# como dispositivo. El de la 2.0.1 (el que ejecuta el instalador oficial de la pantalla) lo deja cargado y
# marcado para eliminar, y entonces no se puede instalar ninguna versión hasta reiniciar.
# Desde la carpeta instalada (sin el paquete) se usa el desinstalador que dejó PawnIO, que es el de la 2.2.0.
function Uninstall-PawnIO {
    $clave = Get-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO' -ErrorAction SilentlyContinue
    if (-not (Get-Service PawnIO -ErrorAction SilentlyContinue) -and -not $clave) { return $true }
    $exe = if (Test-Path -LiteralPath $PkgDrivers) { (Get-InstaladorPawnIO).FullName }
           elseif ($clave.UninstallString -match '^"([^"]+)"|^(\S+)') { "$($matches[1])$($matches[2])" }
    if (-not $exe -or -not (Test-Path -LiteralPath $exe)) { throw 'No se encuentra el desinstalador de PawnIO.' }
    Start-Process $exe -ArgumentList '-uninstall', '-silent' -Wait -WindowStyle Hidden
    -not (Test-PawnIOPendiente)
}

# Detiene el driver PawnIO si está cargado y nadie lo usa (la pantalla ya se ha cerrado). Desinstalado sin
# detenerlo, sigue en memoria: la versión nueva no se carga hasta reiniciar y no se puede borrar del todo.
function Stop-DriverPawnIO {
    $s = Get-Service PawnIO -ErrorAction SilentlyContinue
    if (-not $s -or $s.Status -eq 'Stopped') { return }
    & sc.exe stop PawnIO | Out-Null
    for ($i = 0; $i -lt 20 -and (Get-Service PawnIO -ErrorAction SilentlyContinue).Status -eq 'StopPending'; $i++) { Start-Sleep -Milliseconds 250 }
}

# True si Windows necesita reiniciar para cargar el PawnIO instalado (dispositivo con DN_NEED_RESTART).
function Test-PawnIOReinicio {
    foreach ($d in Get-PnpDevice -InstanceId 'ROOT\PAWNIO\*' -ErrorAction SilentlyContinue) {
        $estado = (Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName DEVPKEY_Device_DevNodeStatus -ErrorAction SilentlyContinue).Data
        if ($estado -band 0x100) { return $true }
    }
    $false
}

# Paquetes de PawnIO en el almacén de drivers de Windows (oemNN.inf).
function Get-PaquetesPawnIO {
    @(Get-WindowsDriver -Online -ErrorAction SilentlyContinue | Where-Object { $_.OriginalFileName -like '*\pawnio.inf' } | ForEach-Object Driver)
}

# Solo al desinstalar: el desinstalador de PawnIO deja su paquete en el almacén de drivers y la clave del
# servicio. (Al reinstalar no se puede: borrar el servicio con el driver cargado impediría instalarlo.)
function Remove-RestosPawnIO {
    foreach ($inf in Get-PaquetesPawnIO) { & pnputil.exe /delete-driver $inf /uninstall /force | Out-Null }
    if (Get-Service PawnIO -ErrorAction SilentlyContinue) { & sc.exe delete PawnIO | Out-Null }
}

# Si el driver sigue cargado, su paquete no se puede borrar hasta reiniciar: una tarea de un solo uso lo
# borra al arrancar Windows (como SYSTEM, con el driver ya descargado) y se elimina a sí misma.
$TareaLimpieza = 'Motherboard Panel - limpieza de PawnIO'
function Register-LimpiezaPawnIO {
    $comandos = @"
Get-WindowsDriver -Online | Where-Object { `$_.OriginalFileName -like '*\pawnio.inf' } | ForEach-Object { pnputil.exe /delete-driver `$_.Driver /uninstall /force }
if (Get-Service PawnIO -ErrorAction SilentlyContinue) { sc.exe delete PawnIO }
Unregister-ScheduledTask -TaskName '$TareaLimpieza' -Confirm:`$false
"@
    $codificado = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($comandos))
    $accion = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand $codificado"
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    Register-ScheduledTask -TaskName $TareaLimpieza -Action $accion -Trigger (New-ScheduledTaskTrigger -AtStartup) -Principal $principal -Force `
        -Description 'Borra los restos del driver PawnIO tras desinstalar Motherboard Panel y se elimina a sí misma.' | Out-Null
}

# Sin rastro de PawnIO: ni servicio (aunque esté marcado para eliminar) ni paquete en el almacén de drivers.
function Test-PawnIOQuitado {
    -not (Test-Path 'HKLM:\SYSTEM\CurrentControlSet\Services\PawnIO') -and -not (Get-PaquetesPawnIO)
}

# Antes de instalar nada: si hay un PawnIO de otra versión, ofrece sustituirlo (Intro = sí).
# El instalador oficial de la pantalla siempre lo cambia por la 2.0.1, así que si se rechaza no se instala nada.
function Confirm-PawnIO {
    if (Test-PawnIOPendiente) { Write-Host $AvisoReinicio -ForegroundColor Yellow; return $false }
    $actual = Get-VersionPawnIO
    if (-not $actual) {
        if (Test-Path 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO') { Write-Host 'Driver PawnIO: instalación incompleta; se reinstalará la versión correcta.' -ForegroundColor Yellow }
        else { Write-Host "Driver PawnIO: no instalado; se instalará la $VersionPawnIO." }
        return $true
    }
    if ($actual -eq $VersionPawnIO) { Write-Host "Driver PawnIO $actual correcto." -ForegroundColor Green; return $true }
    Write-Host "Driver PawnIO instalado: versión $actual (incorrecta, se necesita la $VersionPawnIO)." -ForegroundColor Yellow
    if ((Read-Host "¿Desinstalarlo e instalar la $VersionPawnIO? (S/N, Intro = S)") -match '^\s*$|^[sS]') { return $true }
    Write-Host "Instalación cancelada: la pantalla necesita PawnIO $VersionPawnIO (el instalador oficial de la pantalla lo sustituiría igualmente por la 2.0.1)." -ForegroundColor Yellow
    $false
}

function Install-PawnIO {
    $inst = Get-InstaladorPawnIO
    $actual = Get-VersionPawnIO
    if ($actual -eq $VersionPawnIO) { Write-Host "Driver PawnIO $actual correcto." -ForegroundColor Green; return }
    Write-Host "Driver PawnIO: $(if ($actual) { "el instalador oficial de la pantalla ha puesto la $actual" } else { 'no instalado o incompleto' }) -> se sustituye por la $VersionPawnIO."
    Stop-DriverPawnIO   # el instalador oficial de la pantalla deja la 2.0.1 cargada
    if (-not (Uninstall-PawnIO)) { throw $AvisoReinicio }   # limpia restos de una instalación anterior
    Stop-DriverPawnIO
    $p = Start-Process $inst.FullName -ArgumentList '-install', '-silent' -Wait -PassThru -WindowStyle Hidden
    $final = Get-VersionPawnIO
    if ($final -ne $VersionPawnIO) {
        if (Test-PawnIOPendiente) { throw $AvisoReinicio }
        throw "La instalación de PawnIO no se ha completado (código $($p.ExitCode), versión detectada: $final)."
    }
    if (Test-PawnIOReinicio) { Write-Host "Driver PawnIO $final instalado; Windows necesita reiniciar para cargarlo." -ForegroundColor Yellow }
    else { Write-Host "Driver PawnIO $final instalado." -ForegroundColor Green }
}

# --- Motherboard Panel y tarea -------------------------------------------------

function Register-Tarea([string]$Carpeta) {
    $exe = Join-Path $Carpeta 'Motherboard Panel.exe'
    $accion = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $Carpeta
    $disparador = New-ScheduledTaskTrigger -AtLogOn -User $UsuarioNombre
    # Poco retraso: si la barra de tareas aún no está lista, Motherboard Panel crea su icono en cuanto lo esté.
    $disparador.Delay = 'PT3S'
    $principal = New-ScheduledTaskPrincipal -UserId $UsuarioNombre -LogonType Interactive -RunLevel Highest
    $ajustes = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew
    Register-ScheduledTask -TaskName $Tarea -Action $accion -Trigger $disparador -Principal $principal -Settings $ajustes -Force `
        -Description 'Inicia Motherboard Panel (pantalla USB y luces RGB) al iniciar sesión.' | Out-Null
    Write-Host "Tarea programada '$Tarea' creada." -ForegroundColor Green
}

# Entrada en "Aplicaciones instaladas" que ejecuta una copia de este script con -Accion Desinstalar.
function Register-Desinstalador([string]$Carpeta) {
    $script = Join-Path $Carpeta $ScriptDesinst
    Copy-Item -LiteralPath $PSCommandPath -Destination $script -Force
    $ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $exe = Join-Path $Carpeta 'Motherboard Panel.exe'
    $kb = [int]((Get-ChildItem -LiteralPath $Carpeta -Recurse -File | Measure-Object Length -Sum).Sum / 1KB)
    New-Item -Path $ClaveDesinst -Force | Out-Null
    $valores = [ordered]@{
        DisplayName     = $NombreDesinst
        DisplayVersion  = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
        DisplayIcon     = $exe
        InstallLocation = $Carpeta
        InstallDate     = (Get-Date -Format 'yyyyMMdd')
        UninstallString = "`"$ps`" -NoProfile -ExecutionPolicy Bypass -File `"$script`" -Accion Desinstalar"
    }
    foreach ($v in $valores.GetEnumerator()) { Set-ItemProperty -Path $ClaveDesinst -Name $v.Key -Value $v.Value }
    foreach ($n in 'NoModify', 'NoRepair') { Set-ItemProperty -Path $ClaveDesinst -Name $n -Value 1 -Type DWord }
    Set-ItemProperty -Path $ClaveDesinst -Name EstimatedSize -Value $kb -Type DWord
    Write-Host "Desinstalador '$NombreDesinst' registrado." -ForegroundColor Green
}

# Carpeta de datos (AppData\Local) del usuario que lanzó la instalación.
function Get-DatosUsuario([string]$Nombre) {
    $perfil = (Get-CimInstance Win32_UserProfile -Filter "SID='$UsuarioSid'").LocalPath
    if ($perfil) { Join-Path $perfil "AppData\Local\$Nombre" }
}

function Get-AccesoDirecto { Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Motherboard Panel.lnk' }

function New-AccesoDirecto([string]$Carpeta) {
    $s = (New-Object -ComObject WScript.Shell).CreateShortcut((Get-AccesoDirecto))
    $s.TargetPath = Join-Path $Carpeta 'Motherboard Panel.exe'
    $s.WorkingDirectory = $Carpeta
    $s.Description = 'Pantalla USB y luces RGB'
    $s.Save()
}

# --- Acciones ------------------------------------------------------------------

function Install-Todo {
    # La copia instalada (Desinstalar.ps1) no está junto al paquete: no puede instalar nada.
    if (-not (Test-Path -LiteralPath (Join-Path $PkgPantalla 'Programa corregido'))) {
        throw "Este script no está junto al paquete de instalación (falta $PkgPantalla). Ejecuta 'Instalar Motherboard Panel.cmd' desde el paquete."
    }
    $carpeta = Read-Carpeta
    $anterior = Get-CarpetaInstalada
    if ($anterior -and $anterior -ne $carpeta) {
        throw "Motherboard Panel ya está instalado en $anterior. Para instalarlo en otra carpeta, desinstálalo primero."
    }
    $pantalla = Join-Path $carpeta 'Pantalla'
    $luces = Join-Path $carpeta 'Luces'
    Write-Host "`nSe instalará en: $carpeta" -ForegroundColor Cyan
    if (-not (Confirm-PawnIO)) { return }
    Protect-Carpeta $carpeta
    # Una desinstalación sin reiniciar después dejó pendiente borrar PawnIO al arrancar: ya no debe hacerse.
    if (Get-ScheduledTask -TaskName $TareaLimpieza -ErrorAction SilentlyContinue) { Unregister-ScheduledTask -TaskName $TareaLimpieza -Confirm:$false }

    Write-Host "`n[1/7] Detener lo que esté en marcha" -ForegroundColor Cyan
    if (Test-Path -LiteralPath $carpeta) { Stop-Todo $carpeta }
    if (Get-Service PawnIO -ErrorAction SilentlyContinue) {
        # El instalador oficial de la pantalla quitaría este driver con su desinstalador de la 2.0.1 y lo dejaría a medias.
        Write-Host "Desinstalando el driver PawnIO actual (el instalador oficial de la pantalla pone la 2.0.1 y en el paso 4 se vuelve a instalar la $VersionPawnIO)..."
        Stop-DriverPawnIO
        if (-not (Uninstall-PawnIO)) { throw $AvisoReinicio }
        Write-Host 'Driver PawnIO desinstalado.' -ForegroundColor Green
    }
    Write-Host "`n[2/7] Pantalla oficial 3.10.0 y correcciones" -ForegroundColor Cyan
    Install-PantallaOficial $pantalla
    Install-Correcciones $pantalla
    Install-Temas $pantalla
    Remove-Sobrantes $pantalla
    Set-Configuracion $pantalla
    Write-Host "`n[3/7] LibreHardwareMonitor" -ForegroundColor Cyan
    Install-Lhm $pantalla
    Write-Host "`n[4/7] Driver PawnIO" -ForegroundColor Cyan
    Install-PawnIO
    Write-Host "`n[5/7] Luces RGB" -ForegroundColor Cyan
    Copy-Carpeta $PkgLuces $luces -Excluir 'presets.json', 'helper.cfg'
    Write-Host "Luces instaladas en $luces." -ForegroundColor Green
    Write-Host "`n[6/7] Motherboard Panel" -ForegroundColor Cyan
    Copy-Carpeta $PkgPanel $carpeta
    New-Item -Path $ClaveRegistro -Force | Out-Null
    Set-ItemProperty -Path $ClaveRegistro -Name InstallDir -Value $carpeta
    New-AccesoDirecto $carpeta
    Register-Tarea $carpeta
    Register-Desinstalador $carpeta
    Write-Host "`n[7/7] Comprobación" -ForegroundColor Cyan
    Show-Estado
    if (Test-PawnIOReinicio) {
        Write-Host "`nReinicia Windows para cargar el driver PawnIO $VersionPawnIO; Motherboard Panel arrancará solo al iniciar sesión." -ForegroundColor Yellow
    }
    elseif ((Read-Host "`n¿Iniciar ahora Motherboard Panel? (S/N)") -match '^[sS]') {
        Start-ScheduledTask -TaskName $Tarea
        Write-Host 'Motherboard Panel iniciado.' -ForegroundColor Green
    }
}

# Quita todo: programas, tarea programada, inicio automático, accesos directos, driver PawnIO (y su servicio),
# entradas del registro y, si se pide, los presets de las luces y el registro de Motherboard Panel.
function Uninstall-Todo {
    $carpeta = Get-CarpetaInstalada
    if (-not $carpeta) { $carpeta = (Get-ItemProperty $ClaveDesinst -ErrorAction SilentlyContinue).InstallLocation }
    if (-not $carpeta) {
        Write-Host 'Motherboard Panel no está instalado.' -ForegroundColor Yellow
        if (-not (Test-PawnIOQuitado) -and (Read-Host '¿Quitar los restos del driver PawnIO? (S/N, Intro = S)') -match '^\s*$|^[sS]') {
            try { [void](Uninstall-PawnIO) } catch { }   # sin desinstalador: quedan solo restos
            Remove-RestosPawnIO
            if (Test-PawnIOQuitado) { Write-Host 'Restos de PawnIO quitados.' -ForegroundColor Green }
            else { Register-LimpiezaPawnIO; Write-Host 'Restos de PawnIO: se terminan de borrar solos al reiniciar Windows.' -ForegroundColor Yellow }
        }
        return
    }
    Write-Host "Se desinstalará $NombreDesinst de $carpeta, con el driver PawnIO y la tarea programada." -ForegroundColor Cyan
    if ((Read-Host '¿Continuar? (S/N)') -notmatch '^[sS]') { return }
    $borrarDatos = (Read-Host '¿Borrar también tus presets de luces y el registro de Motherboard Panel? (S/N, Intro = N)') -match '^[sS]'

    Write-Host "`n[1/5] Detener lo que esté en marcha" -ForegroundColor Cyan
    if (Test-Path -LiteralPath $carpeta) { Stop-Todo $carpeta }
    Write-Host "`n[2/5] Tarea programada, inicio automático y accesos directos" -ForegroundColor Cyan
    if (Get-ScheduledTask -TaskName $Tarea -ErrorAction SilentlyContinue) { Unregister-ScheduledTask -TaskName $Tarea -Confirm:$false }
    $run = "Registry::HKEY_USERS\$UsuarioSid\Software\Microsoft\Windows\CurrentVersion\Run"
    $cmd = (Get-ItemProperty $run -ErrorAction SilentlyContinue).RGBControlHelper
    if ($cmd -and $cmd -like "*$carpeta*") { Remove-ItemProperty $run -Name RGBControlHelper }
    $lnk = Get-AccesoDirecto; if (Test-Path -LiteralPath $lnk) { Remove-Item -LiteralPath $lnk }
    Write-Host 'Quitados.' -ForegroundColor Green
    Write-Host "`n[3/5] Pantalla" -ForegroundColor Cyan
    $desinst = Join-Path $carpeta 'Pantalla\unins000.exe'
    if (Test-Path -LiteralPath $desinst) { Start-Process $desinst -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait }
    Remove-RastrosTuring
    Write-Host 'Pantalla desinstalada.' -ForegroundColor Green
    Write-Host "`n[4/5] Driver PawnIO" -ForegroundColor Cyan
    # Un driver cargado no se descarga hasta reiniciar: su servicio sigue existiendo aunque ya esté desinstalado.
    Stop-DriverPawnIO
    try { [void](Uninstall-PawnIO) } catch { }   # sin desinstalador: quedan solo restos
    Stop-DriverPawnIO
    Remove-RestosPawnIO
    $quitado = Test-PawnIOQuitado
    if ($quitado) { Write-Host 'Driver PawnIO, su servicio y su paquete desinstalados.' -ForegroundColor Green }
    else {
        Register-LimpiezaPawnIO
        Write-Host 'Driver PawnIO desinstalado; sigue cargado hasta que reinicies Windows. Al arrancar se borran solos sus restos.' -ForegroundColor Yellow
    }
    Write-Host "`n[5/5] Archivos y registro" -ForegroundColor Cyan
    Set-Location $env:SystemRoot   # por si este script se ejecuta desde la carpeta que se va a borrar
    Start-Sleep -Seconds 2
    if (Test-Path -LiteralPath $carpeta) {
        try { [IO.Directory]::Delete($carpeta, $true) }
        catch { Write-Host "No se ha podido borrar todo $carpeta ($($_.Exception.Message)). Bórrala a mano tras reiniciar." -ForegroundColor Yellow }
    }
    Remove-Item $ClaveRegistro -Recurse -ErrorAction SilentlyContinue
    Remove-Item $ClaveDesinst -Recurse -ErrorAction SilentlyContinue
    if ($borrarDatos) {
        foreach ($d in (Get-DatosUsuario 'RGB Control'), (Get-DatosUsuario 'Motherboard Panel')) {
            if ($d -and (Test-Path -LiteralPath $d)) { Remove-Item -LiteralPath $d -Recurse -Force }
        }
        Remove-Item "Registry::HKEY_USERS\$UsuarioSid\Software\RGBControl" -Recurse -ErrorAction SilentlyContinue
    }
    Write-Host "$NombreDesinst desinstalado." -ForegroundColor Green
    if (-not $quitado) { Write-Host 'Reinicia Windows para terminar de quitar el driver PawnIO (se hace solo al arrancar).' -ForegroundColor Yellow }
}

function Show-Estado {
    $carpeta = Get-CarpetaInstalada
    Write-Host 'Motherboard Panel:     ' -NoNewline
    if ($carpeta) { Write-Host $carpeta -ForegroundColor Green } else { Write-Host 'no instalado' -ForegroundColor Yellow }
    if ($carpeta) {
        $pantalla = Join-Path $carpeta 'Pantalla'
        $v = Get-VersionTexto $pantalla
        $esperada = Get-VersionTexto (Join-Path $PkgPantalla 'Programa corregido')
        Write-Host 'Pantalla:              ' -NoNewline
        if ($v -eq $esperada) { Write-Host "$v (con las correcciones)" -ForegroundColor Green } elseif ($v) { Write-Host "$v (sin las correcciones del paquete: $esperada)" -ForegroundColor Yellow } else { Write-Host 'no instalada' -ForegroundColor Yellow }
        Write-Host 'LibreHardwareMonitor:  ' -NoNewline
        $l = Test-Lhm $pantalla
        if ($l -eq 'correcto') { Write-Host "$VersionLhm (correcto)" -ForegroundColor Green } else { Write-Host $l -ForegroundColor Yellow }
        Write-Host 'Luces RGB:             ' -NoNewline
        if (Test-Path -LiteralPath (Join-Path $carpeta 'Luces\RGB Helper.exe')) { Write-Host (Join-Path $carpeta 'Luces') -ForegroundColor Green } else { Write-Host 'no instaladas' -ForegroundColor Yellow }
    }
    Write-Host 'Driver PawnIO:         ' -NoNewline
    $pw = Get-VersionPawnIO
    if (Test-PawnIOPendiente) { Write-Host 'pendiente de eliminarse (reinicia Windows)' -ForegroundColor Yellow } elseif ($pw -eq $VersionPawnIO -and (Test-PawnIOReinicio)) { Write-Host "$pw (correcto; falta reiniciar Windows para cargarlo)" -ForegroundColor Yellow } elseif ($pw -eq $VersionPawnIO) { Write-Host "$pw (correcto)" -ForegroundColor Green } elseif ($pw) { Write-Host "$pw (se necesita la $VersionPawnIO)" -ForegroundColor Yellow } else { Write-Host 'no instalado' -ForegroundColor Yellow }
    Write-Host 'Tarea programada:      ' -NoNewline
    if (Get-ScheduledTask -TaskName $Tarea -ErrorAction SilentlyContinue) { Write-Host 'creada' -ForegroundColor Green } else { Write-Host 'no existe' -ForegroundColor Yellow }
    Write-Host 'Desinstalador:         ' -NoNewline
    if (Test-Path $ClaveDesinst) { Write-Host "registrado ('$NombreDesinst')" -ForegroundColor Green } else { Write-Host 'no registrado' -ForegroundColor Yellow }
}

function Invoke-Seguro([scriptblock]$Bloque) {
    try { & $Bloque } catch { Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red }
}

switch ($Accion) {
    'Estado'      { Show-Estado; return }
    'Instalar'    { Invoke-Seguro { Install-Todo }; Read-Host "`nPulsa Intro para salir" | Out-Null; return }
    'Desinstalar' { Invoke-Seguro { Uninstall-Todo }; Read-Host "`nPulsa Intro para salir" | Out-Null; return }
}

do {
    Clear-Host
    Write-Host '==================================================' -ForegroundColor Cyan
    Write-Host '  Motherboard Panel - instalación' -ForegroundColor Cyan
    Write-Host '==================================================' -ForegroundColor Cyan
    Write-Host "Usuario:               $UsuarioNombre"
    Invoke-Seguro { Show-Estado }
    Write-Host ''
    Write-Host '  1) Instalar Motherboard Panel (pantalla con correcciones, luces y drivers)'
    Write-Host '  2) Desinstalar todo'
    Write-Host '  3) Salir sin hacer nada'
    Write-Host ''
    $opcion = (Read-Host 'Elige una opción (1-3)').Trim()
    switch ($opcion) {
        '1' { Write-Host ''; Invoke-Seguro { Install-Todo };   Read-Host "`nPulsa Intro para volver al menú" | Out-Null }
        '2' { Write-Host ''; Invoke-Seguro { Uninstall-Todo }; Read-Host "`nPulsa Intro para volver al menú" | Out-Null }
        '3' { }
        default { Write-Host 'Opción no válida.' -ForegroundColor Yellow; Start-Sleep -Seconds 1 }
    }
} while ($opcion -ne '3')
