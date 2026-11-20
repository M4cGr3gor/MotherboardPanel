# Pruebas de comportamiento de Motherboard Panel contra instalaciones de prueba (no toca lo instalado):
#   build\prueba-oficial  pantalla 3.10.0 + correcciones, con REVISION: SIMU y HW_SENSORS: PYTHON
#   build\prueba-helper   RGB Helper.exe (copia, sin helper.cfg: no toca las luces) y RGB Control.exe
#   build\prueba-panel    Motherboard Panel.exe con un .ini que apunta a las dos carpetas anteriores
$ErrorActionPreference = 'Stop'
# Sin elevar, Motherboard Panel lanza la tarea programada "Motherboard Panel" y se cierra: con el programa
# instalado, la prueba arrancaría el panel real en lugar del de prueba.
if (Get-ScheduledTask -TaskName 'Motherboard Panel' -ErrorAction SilentlyContinue) {
    Write-Host 'Motherboard Panel está instalado (existe su tarea programada): estas pruebas necesitan un equipo sin él. Desinstálalo antes.' -ForegroundColor Yellow
    exit 1
}
$b = Join-Path $PSScriptRoot '..\build' | Resolve-Path
$panelExe = "$b\prueba-panel\Motherboard Panel.exe"
$mainExe = "$b\prueba-oficial\main.exe"
$helperExe = "$b\prueba-helper\RGB Helper.exe"
$log = Join-Path $env:LOCALAPPDATA 'Motherboard Panel\registro.log'

Add-Type -Namespace MP -Name T -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)] public struct NII { public int cbSize; public IntPtr hWnd; public uint uID; public Guid guidItem; }
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int l, t, r, b; }
[DllImport("shell32.dll")] public static extern int Shell_NotifyIconGetRect(ref NII id, out RECT r);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string t);
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
[DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint a, bool i, int pid);
[DllImport("kernel32.dll")] public static extern bool TerminateProcess(IntPtr h, uint code);
[DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
public static bool Icon(string cls) {
  IntPtr h = FindWindow(cls, null); if (h == IntPtr.Zero) return false;
  NII id = new NII(); id.cbSize = Marshal.SizeOf(typeof(NII)); id.hWnd = h; id.uID = 1; RECT r;
  return Shell_NotifyIconGetRect(ref id, out r) == 0;
}
public static void Command(int id) { PostMessage(FindWindow("MotherboardPanelWindow", null), 0x0111, (IntPtr)id, IntPtr.Zero); }
public static void Kill(int pid, uint code) { IntPtr h = OpenProcess(0x0001, false, pid); TerminateProcess(h, code); CloseHandle(h); }
'@
$ID_SCREEN_CONFIG = 100; $ID_EXIT = 106

# La coma evita que PowerShell desenvuelva un array de un solo elemento (un CimInstance suelto no tiene .Count).
function Procs($exe) { , @(Get-CimInstance Win32_Process -Filter "Name='$(Split-Path $exe -Leaf)'" | Where-Object { $_.ExecutablePath -eq $exe }) }
function WaitFor([scriptblock]$cond, [int]$seconds) { for ($i = 0; $i -lt $seconds * 2; $i++) { if (& $cond) { return $true }; Start-Sleep -Milliseconds 500 }; & $cond }
function LogSince($mark) { if (Test-Path $log) { Get-Content $log -Encoding UTF8 | Select-Object -Skip $mark } }
function LogCount { if (Test-Path $log) { @(Get-Content $log -Encoding UTF8).Count } else { 0 } }
$resultados = @()
function Check($nombre, $ok) { $script:resultados += [pscustomobject]@{ Prueba = $nombre; Resultado = $(if ($ok) { 'OK' } else { 'FALLO' }) } }

# Limpieza previa
foreach ($e in $panelExe, $mainExe, $helperExe, "$b\prueba-oficial\configure.exe") { (Procs $e) | ForEach-Object { Stop-Process -Id $_.ProcessId -Force } }
Start-Sleep -Seconds 1

# --- startup: arranca la pantalla, sin su icono; un solo icono (el del panel)
$m = LogCount
Start-Process $panelExe | Out-Null
$okMain = WaitFor { (Procs $mainExe).Count -eq 1 } 10
$okPanelIcon = WaitFor { [MP.T]::Icon('MotherboardPanelWindow') } 10
Start-Sleep -Seconds 20
$mainLog = Get-Content "$b\prueba-oficial\log.log" -ErrorAction SilentlyContinue
Check 'startup: pantalla arrancada' $okMain
Check 'startup: icono de Motherboard Panel' $okPanelIcon
Check 'startup: la pantalla oculta su icono' ([bool]($mainLog -match 'Motherboard Panel detected: tray icon hidden'))

# --- screen_crash: la pantalla termina con error y se reinicia
$pid1 = (Procs $mainExe)[0].ProcessId
[MP.T]::Kill($pid1, 1)
$okRestart = WaitFor { $p = Procs $mainExe; $p.Count -eq 1 -and $p[0].ProcessId -ne $pid1 } 8
Check 'screen_crash: reiniciada tras cerrarse con error' $okRestart

# --- config_cancel: abrir el configurador y cerrarlo sin guardar
Start-Sleep -Seconds 15
[MP.T]::Command($ID_SCREEN_CONFIG)
$okCfg = WaitFor { (Procs "$b\prueba-oficial\configure.exe").Count -ge 1 -and (Procs $mainExe).Count -eq 0 } 20
Check 'config: pantalla detenida y configurador abierto' $okCfg
(Procs "$b\prueba-oficial\configure.exe") | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
$okBack = WaitFor { (Procs $mainExe).Count -eq 1 } 15
Check 'config_cancel: la pantalla vuelve a arrancar' $okBack

# --- config_save_and_run: el configurador arranca la pantalla ("Save and run") y el panel no la duplica
Start-Sleep -Seconds 15
[MP.T]::Command($ID_SCREEN_CONFIG)
WaitFor { (Procs "$b\prueba-oficial\configure.exe").Count -ge 1 -and (Procs $mainExe).Count -eq 0 } 20 | Out-Null
(Procs "$b\prueba-oficial\configure.exe") | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Start-Process $mainExe -WorkingDirectory "$b\prueba-oficial" | Out-Null   # lo que hace "Save and run"
Start-Sleep -Seconds 12
Check 'config_save_and_run: una sola pantalla' ((Procs $mainExe).Count -eq 1)

# --- rgb: cierre normal (código 0) no se reinicia; fallo (0xC0000005) sí
Start-Process $helperExe -WorkingDirectory "$b\prueba-helper" | Out-Null
WaitFor { (Procs $helperExe).Count -eq 1 } 5 | Out-Null
Start-Sleep -Seconds 3
Check 'rgb: el ayudante oculta su icono' (-not [MP.T]::Icon('RGBControlHelperWindow'))
$ev = [System.Threading.EventWaitHandle]::OpenExisting('RGBControl.Exit'); $ev.Set() | Out-Null; $ev.Dispose()
Start-Sleep -Seconds 6
Check 'rgb_normal_effect: no se reinicia tras cerrarse normalmente' ((Procs $helperExe).Count -eq 0)
Start-Process $helperExe -WorkingDirectory "$b\prueba-helper" | Out-Null
WaitFor { (Procs $helperExe).Count -eq 1 } 5 | Out-Null
Start-Sleep -Seconds 3
$hp = (Procs $helperExe)[0].ProcessId
[MP.T]::Kill($hp, [uint32]3221225477)   # 0xC0000005
$okRgb = WaitFor { $p = Procs $helperExe; $p.Count -eq 1 -and $p[0].ProcessId -ne $hp } 8
Check 'rgb_crash: el ayudante se reinicia tras un fallo' $okRgb

# --- exit: apaga la pantalla limpiamente y cierra el ayudante
$mp = (Procs $mainExe)[0].ProcessId
$mh = Get-Process -Id $mp
$null = $mh.Handle   # abrir el proceso ahora para poder leer su código de salida cuando termine
[MP.T]::Command($ID_EXIT)
$okExit = WaitFor { (Procs $panelExe).Count -eq 0 -and (Procs $mainExe).Count -eq 0 -and (Procs $helperExe).Count -eq 0 } 20
Check 'exit: todo cerrado' $okExit
Check 'exit: la pantalla se cerró limpiamente (código 0)' ($mh.HasExited -and $mh.ExitCode -eq 0)
Check 'exit: sin icono del panel' (-not [MP.T]::Icon('MotherboardPanelWindow'))

"--- Registro de Motherboard Panel ---"
LogSince $m
"--- Resultados ---"
$resultados | Format-Table -AutoSize | Out-String
"$(@($resultados | Where-Object Resultado -eq 'OK').Count)/$($resultados.Count) pruebas superadas"
