# Abre la ventana un instante, la dibuja en un PNG y la cierra (para revisar el diseño).
# -Demo añade dos presets de usuario de ejemplo; -Picker captura además el selector de iconos.
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\src\RgbApp\bin\Release\net48\RGB Control.exe'),
    [string]$Out = (Join-Path $PSScriptRoot 'ui.png'),
    [int]$SelectIndex = 0,
    [switch]$Demo,
    [string]$PickerOut
)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path $Exe))
$form = [Activator]::CreateInstance($asm.GetType('RgbApp.MainForm'))
$flags = [Reflection.BindingFlags]'NonPublic,Instance'

if ($Demo) {
    $store = $asm.GetType('RgbApp.MainForm').GetField('store', $flags).GetValue($form)
    $presetType = $asm.GetType('RgbCore.Preset')
    $a = [Activator]::CreateInstance($presetType); $a.Name = 'Rojo gamer'; $a.Effect = 2; $a.Color = '#FF1030'; $a.Icon = 'heart'
    $b = [Activator]::CreateInstance($presetType); $b.Name = 'Noche'; $b.Effect = 1; $b.Color = '#6040FF'; $b.Brightness = 40; $b.Icon = 'moon'
    $store.Presets.Add($a); $store.Presets.Add($b)
    $samples = @(
        @{ N = 'Atardecer'; E = 7; C = @('#FF4000', '#FF0080', '#8000FF') },
        @{ N = 'Océano'; E = 8; C = @('#0020FF', '#00C0FF', '#00FFA0') },
        @{ N = 'Mis colores'; E = 9; C = @('#FF0000', '#00FF00', '#0000FF', '#FFFF00') },
        @{ N = 'Arcoíris rápido'; E = 10; C = $null },
        @{ N = 'Cometa naranja'; E = 11; C = $null },
        @{ N = 'Respira'; E = 12; C = @('#FF0040', '#4000FF') },
        @{ N = 'Estrellas'; E = 13; C = @('#FFFFFF', '#80C0FF') },
        @{ N = 'Hoguera'; E = 14; C = $null },
        @{ N = 'Velita'; E = 15; C = $null },
        @{ N = 'Pulso'; E = 16; C = $null },
        @{ N = 'Boreal'; E = 17; C = @('#00FF80', '#0080FF', '#8000FF') },
        @{ N = 'KITT'; E = 18; C = $null },
        @{ N = 'Feria'; E = 19; C = @('#FF0000', '#FFFF00', '#00FF00') },
        @{ N = 'Duelo'; E = 20; C = @('#FF0000', '#0000FF') },
        @{ N = 'Rayos'; E = 21; C = $null }
    )
    foreach ($d in $samples) {
        $p = [Activator]::CreateInstance($presetType); $p.Name = $d.N; $p.Effect = $d.E; $p.Color = '#FF8000'
        if ($d.C) { $p.Colors = [System.Collections.Generic.List[string]]$d.C }
        $store.Presets.Add($p)
    }
    $asm.GetType('RgbApp.MainForm').GetMethod('RefreshList', $flags).Invoke($form, @('Rojo gamer')) | Out-Null
}

$form.Show()
$list = $form.Controls[0].GetControlFromPosition(0, 0).Controls[1]
if ($SelectIndex -ge 0 -and $SelectIndex -lt $list.Items.Count) { $list.SelectedIndex = $SelectIndex }
for ($i = 0; $i -lt 20; $i++) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 25 }
$b = $form.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$form.DrawToBitmap($bmp, (New-Object System.Drawing.Rectangle 0, 0, $b.Width, $b.Height))
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "Captura: $Out"

if ($PickerOut) {
    $accent = [System.Drawing.Color]::FromArgb(0, 103, 192)
    $sel = [System.Drawing.Color]::FromArgb(224, 236, 248)
    $picker = [Activator]::CreateInstance($asm.GetType('RgbApp.IconPickerForm'), @($list.SelectedItem, $accent, $sel))
    $picker.Show()
    for ($i = 0; $i -lt 20; $i++) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 25 }
    $pb = $picker.Bounds
    $bmp2 = New-Object System.Drawing.Bitmap $pb.Width, $pb.Height
    $picker.DrawToBitmap($bmp2, (New-Object System.Drawing.Rectangle 0, 0, $pb.Width, $pb.Height))
    $bmp2.Save($PickerOut, [System.Drawing.Imaging.ImageFormat]::Png)
    $picker.Close()
    Write-Output "Selector: $PickerOut"
}
$form.Close()
