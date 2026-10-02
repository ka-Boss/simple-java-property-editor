$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repo "publish\SimpleJavaPropertyEditor.exe"
if (-not (Test-Path $exe)) {
  throw "EXE がありません: $exe"
}

$layout = Join-Path $repo "packaging\layout"
$assets = Join-Path $layout "Assets"
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Path $assets | Out-Null

Copy-Item (Join-Path $PSScriptRoot "AppxManifest.xml") (Join-Path $layout "AppxManifest.xml")
Copy-Item $exe (Join-Path $layout "SimpleJavaPropertyEditor.exe")

Add-Type -AssemblyName System.Drawing
$icon = New-Object System.Drawing.Icon (Join-Path $repo "PropertyEditor\icon.ico")
$source = $icon.ToBitmap()

function Save-Logo([int]$size, [string]$name) {
  $bitmap = New-Object System.Drawing.Bitmap $size, $size
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $graphics.Clear([System.Drawing.Color]::Transparent)
  $graphics.DrawImage($source, 0, 0, $size, $size)
  $bitmap.Save((Join-Path $assets $name), [System.Drawing.Imaging.ImageFormat]::Png)
  $graphics.Dispose()
  $bitmap.Dispose()
}

Save-Logo 50 "StoreLogo.png"
Save-Logo 44 "Square44x44Logo.png"
Save-Logo 150 "Square150x150Logo.png"
$source.Dispose()
$icon.Dispose()

$kitRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
$kit = Get-ChildItem $kitRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
if (-not $kit) { throw "Windows SDK が見つかりません" }
$makeappx = Join-Path $kit.FullName "x64\makeappx.exe"
$signtool = Join-Path $kit.FullName "x64\signtool.exe"

$msix = Join-Path $repo "packaging\SimpleJavaPropertyEditor.msix"
if (Test-Path $msix) { Remove-Item $msix -Force }
& $makeappx pack /d $layout /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx が失敗しました" }

$subject = "CN=E0B072E1-D4AE-43D9-BF2A-6DF494066C5E"
$cert = New-SelfSignedCertificate -Type Custom -Subject $subject -KeyUsage DigitalSignature -FriendlyName "simple Java Property Editor store upload" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")
$pfx = Join-Path $repo "packaging\store-upload.pfx"
$pass = [Guid]::NewGuid().ToString("N")
$secure = ConvertTo-SecureString -String $pass -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $secure | Out-Null

& $signtool sign /fd SHA256 /f $pfx /p $pass /tr http://timestamp.acs.microsoft.com /td SHA256 $msix
$signExit = $LASTEXITCODE
Remove-Item $pfx -Force
Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force
if ($signExit -ne 0) { throw "signtool が失敗しました" }

Write-Output "OK $msix"
