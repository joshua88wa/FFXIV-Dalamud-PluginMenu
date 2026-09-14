Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$target = 'C:\Utilities\FFXIV\repos\FFXIV-Dalamud-PluginMenu\images\screenshot.png'
$img = [System.Windows.Forms.Clipboard]::GetImage()

if ($null -eq $img) {
    'no image on clipboard'
    return
}

$img.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
"saved {0} ({1}x{2})" -f $target, $img.Width, $img.Height
$img.Dispose()
