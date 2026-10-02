$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repo "publish\SimpleJavaPropertyEditor.exe"
if (-not (Test-Path $exe)) {
  throw "EXE not found: $exe"
}

$layout = Join-Path $repo "packaging\layout"
$assets = Join-Path $layout "Assets"
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Path $assets | Out-Null

Copy-Item (Join-Path $PSScriptRoot "AppxManifest.xml") (Join-Path $layout "AppxManifest.xml")
Copy-Item $exe (Join-Path $layout "SimpleJavaPropertyEditor.exe")

function Get-Crc32([byte[]]$Bytes) {
  [uint32]$crc = 4294967295
  foreach ($b in $Bytes) {
    $crc = $crc -bxor [uint32]$b
    for ($i = 0; $i -lt 8; $i++) {
      if (($crc -band 1) -ne 0) {
        $crc = [uint32](($crc -shr 1) -bxor 3988292384)
      } else {
        $crc = [uint32]($crc -shr 1)
      }
    }
  }
  return [uint32]($crc -bxor 4294967295)
}

function Get-U32Be([uint32]$Value) {
  $bytes = [BitConverter]::GetBytes($Value)
  [Array]::Reverse($bytes)
  return $bytes
}

function Add-PngChunk([System.IO.MemoryStream]$Out, [string]$Type, [byte[]]$Data) {
  $typeBytes = [Text.Encoding]::ASCII.GetBytes($Type)
  $crcInput = New-Object byte[] (4 + $Data.Length)
  [Array]::Copy($typeBytes, 0, $crcInput, 0, 4)
  if ($Data.Length -gt 0) { [Array]::Copy($Data, 0, $crcInput, 4, $Data.Length) }
  $Out.Write((Get-U32Be ([uint32]$Data.Length)), 0, 4)
  $Out.Write($typeBytes, 0, 4)
  if ($Data.Length -gt 0) { $Out.Write($Data, 0, $Data.Length) }
  $Out.Write((Get-U32Be (Get-Crc32 $crcInput)), 0, 4)
}

function Write-SolidPng([int]$Size, [string]$Path) {
  $raw = New-Object System.Collections.Generic.List[byte]
  for ($y = 0; $y -lt $Size; $y++) {
    $raw.Add(0)
    for ($x = 0; $x -lt $Size; $x++) {
      $raw.Add(27)
      $raw.Add(79)
      $raw.Add(114)
    }
  }
  $scan = $raw.ToArray()

  $deflate = New-Object System.IO.MemoryStream
  $compressor = New-Object System.IO.Compression.DeflateStream($deflate, [System.IO.Compression.CompressionLevel]::Fastest, $true)
  $compressor.Write($scan, 0, $scan.Length)
  $compressor.Dispose()
  $compressed = $deflate.ToArray()

  [uint32]$a = 1
  [uint32]$b = 0
  foreach ($byte in $scan) {
    $a = [uint32](($a + $byte) % 65521)
    $b = [uint32](($b + $a) % 65521)
  }
  $adler = [uint32](($b -shl 16) -bor $a)

  $idat = New-Object System.IO.MemoryStream
  $idat.WriteByte(120)
  $idat.WriteByte(1)
  $idat.Write($compressed, 0, $compressed.Length)
  $adlerBytes = Get-U32Be $adler
  $idat.Write($adlerBytes, 0, 4)

  $ihdr = New-Object System.IO.MemoryStream
  $ihdr.Write((Get-U32Be ([uint32]$Size)), 0, 4)
  $ihdr.Write((Get-U32Be ([uint32]$Size)), 0, 4)
  $ihdr.WriteByte(8)
  $ihdr.WriteByte(2)
  $ihdr.WriteByte(0)
  $ihdr.WriteByte(0)
  $ihdr.WriteByte(0)

  $png = New-Object System.IO.MemoryStream
  $sig = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
  $png.Write($sig, 0, $sig.Length)
  Add-PngChunk $png "IHDR" $ihdr.ToArray()
  Add-PngChunk $png "IDAT" $idat.ToArray()
  Add-PngChunk $png "IEND" ([byte[]]@())
  [IO.File]::WriteAllBytes($Path, $png.ToArray())
}

$usedIcon = $false
try {
  Add-Type -AssemblyName System.Drawing -ErrorAction Stop
  $icon = New-Object System.Drawing.Icon (Join-Path $repo "PropertyEditor\icon.ico")
  $source = $icon.ToBitmap()
  foreach ($pair in @(
      @{ Size = 50; Name = "StoreLogo.png" },
      @{ Size = 44; Name = "Square44x44Logo.png" },
      @{ Size = 150; Name = "Square150x150Logo.png" }
    )) {
    $bitmap = New-Object System.Drawing.Bitmap ([int]$pair.Size), ([int]$pair.Size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.DrawImage($source, 0, 0, [int]$pair.Size, [int]$pair.Size)
    $bitmap.Save((Join-Path $assets $pair.Name), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
  }
  $source.Dispose()
  $icon.Dispose()
  $usedIcon = $true
  Write-Output "logos from icon.ico"
} catch {
  Write-Output "icon draw skipped: $($_.Exception.Message)"
  Write-SolidPng 50 (Join-Path $assets "StoreLogo.png")
  Write-SolidPng 44 (Join-Path $assets "Square44x44Logo.png")
  Write-SolidPng 150 (Join-Path $assets "Square150x150Logo.png")
}

if (-not $usedIcon) { Write-Output "logos are solid png" }

$roots = @(
  "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
  "$env:ProgramFiles\Windows Kits\10\bin"
)
$makeappxItem = $null
foreach ($root in $roots) {
  if (-not (Test-Path $root)) { continue }
  $makeappxItem = Get-ChildItem $root -Filter makeappx.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\makeappx\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1
  if ($makeappxItem) { break }
}
if (-not $makeappxItem) { throw "makeappx.exe was not found" }
$makeappx = $makeappxItem.FullName
$signtool = Join-Path (Split-Path $makeappx) "signtool.exe"
Write-Output "makeappx $makeappx"

$msix = Join-Path $repo "packaging\SimpleJavaPropertyEditor.msix"
if (Test-Path $msix) { Remove-Item $msix -Force }
& $makeappx pack /d $layout /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed: $LASTEXITCODE" }

$subject = "CN=E0B072E1-D4AE-43D9-BF2A-6DF494066C5E"
$cert = New-SelfSignedCertificate -Type Custom -Subject $subject -KeyUsage DigitalSignature -FriendlyName "simple Java Property Editor store upload" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")
$pfx = Join-Path $repo "packaging\store-upload.pfx"
$pass = [Guid]::NewGuid().ToString("N")
$secure = ConvertTo-SecureString -String $pass -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $secure | Out-Null

& $signtool sign /fd SHA256 /f $pfx /p $pass /tr http://timestamp.acs.microsoft.com /td SHA256 $msix
if ($LASTEXITCODE -ne 0) {
  Write-Output "timestamp failed, signing without it"
  & $signtool sign /fd SHA256 /f $pfx /p $pass $msix
}
$signExit = $LASTEXITCODE
Remove-Item $pfx -Force -ErrorAction SilentlyContinue
Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force -ErrorAction SilentlyContinue
if ($signExit -ne 0) { throw "signtool failed: $signExit" }

Write-Output "OK $msix"
