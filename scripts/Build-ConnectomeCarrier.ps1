[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $InputPath = 'Mod\connectome\malecns-v1.0.flyb.gz',
    [string] $OutputPath = 'Mod\connectome\malecns-v1.0.png',
    [ValidateRange(1, 16384)]
    [int] $Width = 4096,
    [int] $Height = 0,
    [string] $ExpectedSha256
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
function Resolve-InputPath([string] $Path) {
    if ([IO.Path]::IsPathRooted($Path)) {
        return [IO.Path]::GetFullPath($Path)
    }

    return [IO.Path]::GetFullPath((Join-Path $repositoryRoot $Path))
}

$sourcePath = Resolve-InputPath $InputPath
$destinationPath = Resolve-InputPath $OutputPath

if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Connectome payload was not found: $sourcePath"
}

$payload = [IO.File]::ReadAllBytes($sourcePath)
if ($payload.Length -eq 0) {
    throw 'Connectome payload is empty.'
}

$sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
if ($ExpectedSha256 -and $sourceHash -ne $ExpectedSha256.ToUpperInvariant()) {
    throw "Input SHA-256 does not match the expected value. Actual: $sourceHash"
}

$requiredPixels = [math]::Ceiling($payload.Length / 3.0)
if ($Height -le 0) {
    $Height = [math]::Ceiling($requiredPixels / [double]$Width)
}

$capacity = [long]$Width * $Height * 3
if ($capacity -lt $payload.Length) {
    throw "Carrier capacity ($capacity bytes) is smaller than the payload ($($payload.Length) bytes)."
}

function ConvertTo-BigEndianBytes([uint32] $Value) {
    return ,([byte[]] @(
        [byte](($Value -shr 24) -band 0xff),
        [byte](($Value -shr 16) -band 0xff),
        [byte](($Value -shr 8) -band 0xff),
        [byte]($Value -band 0xff)
    ))
}

function Get-Crc32([byte[]] $Data) {
    [uint32]$crc = [uint32]::MaxValue
    foreach ($value in $Data) {
        $index = [int](($crc -bxor [uint32]$value) -band 0xff)
        $crc = ($crc -shr 8) -bxor $script:CrcTable[$index]
    }

    return $crc -bxor 0xffffffff
}

function Get-Adler32([byte[]] $Data) {
    [uint32]$a = 1
    [uint32]$b = 0
    foreach ($value in $Data) {
        $a = ($a + $value) % 65521
        $b = ($b + $a) % 65521
    }

    return ($b -shl 16) -bor $a
}

function Write-PngChunk([IO.BinaryWriter] $Writer, [string] $Type, [byte[]] $Data) {
    $typeBytes = [Text.Encoding]::ASCII.GetBytes($Type)
    $Writer.Write((ConvertTo-BigEndianBytes ([uint32]$Data.Length)))
    $Writer.Write($typeBytes)
    $Writer.Write($Data)

    $crcInput = [byte[]]::new($typeBytes.Length + $Data.Length)
    [Buffer]::BlockCopy($typeBytes, 0, $crcInput, 0, $typeBytes.Length)
    [Buffer]::BlockCopy($Data, 0, $crcInput, $typeBytes.Length, $Data.Length)
    $Writer.Write((ConvertTo-BigEndianBytes (Get-Crc32 $crcInput)))
}

function Read-BigEndianUInt32([byte[]] $Data, [ref] $Offset) {
    $value = ([uint32]$Data[$Offset.Value] -shl 24) -bor
        ([uint32]$Data[$Offset.Value + 1] -shl 16) -bor
        ([uint32]$Data[$Offset.Value + 2] -shl 8) -bor
        [uint32]$Data[$Offset.Value + 3]
    $Offset.Value += 4
    return $value
}

$script:CrcTable = [uint32[]]::new(256)
for ($i = 0; $i -lt 256; $i++) {
    [uint32]$value = [uint32]$i
    for ($bit = 0; $bit -lt 8; $bit++) {
        $value = if (($value -band 1) -ne 0) { ($value -shr 1) -bxor 0xedb88320 } else { $value -shr 1 }
    }
    $script:CrcTable[$i] = $value
}

$scanlineLength = 1 + ($Width * 3)
$scanlines = [byte[]]::new($scanlineLength * $Height)
for ($row = 0; $row -lt $Height; $row++) {
    $rowOffset = $row * $scanlineLength
    $scanlines[$rowOffset] = 0
    for ($column = 0; $column -lt $Width; $column++) {
        $payloadOffset = (($row * $Width) + $column) * 3
        $pixelOffset = $rowOffset + 1 + ($column * 3)
        for ($channel = 0; $channel -lt 3; $channel++) {
            if ($payloadOffset + $channel -lt $payload.Length) {
                $scanlines[$pixelOffset + $channel] = $payload[$payloadOffset + $channel]
            }
        }
    }
}

$compressed = [IO.MemoryStream]::new()
$compressed.WriteByte(0x78)
$compressed.WriteByte(0x9c)
$deflate = [IO.Compression.DeflateStream]::new($compressed, [IO.Compression.CompressionLevel]::Optimal, $true)
$deflate.Write($scanlines, 0, $scanlines.Length)
$deflate.Dispose()
$adler = ConvertTo-BigEndianBytes (Get-Adler32 $scanlines)
$compressed.Write($adler, 0, $adler.Length)
$idat = $compressed.ToArray()
$compressed.Dispose()

$ihdr = [byte[]]::new(13)
[Buffer]::BlockCopy((ConvertTo-BigEndianBytes ([uint32]$Width)), 0, $ihdr, 0, 4)
[Buffer]::BlockCopy((ConvertTo-BigEndianBytes ([uint32]$Height)), 0, $ihdr, 4, 4)
$ihdr[8] = 8
$ihdr[9] = 2

$png = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($png)
$writer.Write([byte[]]@(137, 80, 78, 71, 13, 10, 26, 10))
Write-PngChunk $writer 'IHDR' $ihdr
Write-PngChunk $writer 'IDAT' $idat
Write-PngChunk $writer 'IEND' ([byte[]]::new(0))
$writer.Flush()
$pngBytes = $png.ToArray()
$writer.Dispose()
$png.Dispose()

if ($PSCmdlet.ShouldProcess($destinationPath, 'write connectome PNG carrier')) {
    $destinationDirectory = Split-Path -Parent $destinationPath
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    [IO.File]::WriteAllBytes($destinationPath, $pngBytes)

    $encoded = [IO.File]::ReadAllBytes($destinationPath)
    $signature = [byte[]]@(137, 80, 78, 71, 13, 10, 26, 10)
    for ($i = 0; $i -lt $signature.Length; $i++) {
        if ($encoded[$i] -ne $signature[$i]) { throw 'Generated carrier is not a valid PNG.' }
    }

    $offset = 8
    $decodedIdat = [IO.MemoryStream]::new()
    $decodedWidth = 0
    $decodedHeight = 0
    while ($offset -lt $encoded.Length) {
        $length = Read-BigEndianUInt32 $encoded ([ref]$offset)
        $type = [Text.Encoding]::ASCII.GetString($encoded, $offset, 4)
        $offset += 4
        $data = [byte[]]::new($length)
        [Buffer]::BlockCopy($encoded, $offset, $data, 0, $length)
        $offset += $length
        $expectedCrc = Read-BigEndianUInt32 $encoded ([ref]$offset)

        $typeBytes = [Text.Encoding]::ASCII.GetBytes($type)
        $crcInput = [byte[]]::new($typeBytes.Length + $data.Length)
        [Buffer]::BlockCopy($typeBytes, 0, $crcInput, 0, $typeBytes.Length)
        [Buffer]::BlockCopy($data, 0, $crcInput, $typeBytes.Length, $data.Length)
        if ($expectedCrc -ne (Get-Crc32 $crcInput)) { throw "Generated carrier has an invalid $type CRC." }

        if ($type -eq 'IHDR') {
            $ihdrOffset = 0
            $decodedWidth = Read-BigEndianUInt32 $data ([ref]$ihdrOffset)
            $decodedHeight = Read-BigEndianUInt32 $data ([ref]$ihdrOffset)
        } elseif ($type -eq 'IDAT') {
            $decodedIdat.Write($data, 0, $data.Length)
        } elseif ($type -eq 'IEND') {
            break
        }
    }

    $zlib = $decodedIdat.ToArray()
    $decodedIdat.Dispose()
    $deflateInput = [IO.MemoryStream]::new($zlib, 2, $zlib.Length - 6, $false)
    $decoder = [IO.Compression.DeflateStream]::new($deflateInput, [IO.Compression.CompressionMode]::Decompress)
    $decodedScanlines = [byte[]]::new($scanlines.Length)
    $read = 0
    while ($read -lt $decodedScanlines.Length) {
        $count = $decoder.Read($decodedScanlines, $read, $decodedScanlines.Length - $read)
        if ($count -eq 0) { break }
        $read += $count
    }
    $decoder.Dispose()
    $deflateInput.Dispose()
    if ($decodedWidth -ne $Width -or $decodedHeight -ne $Height -or $read -ne $decodedScanlines.Length) {
        throw 'Generated carrier dimensions or decompressed data length is invalid.'
    }

    $reconstructed = [byte[]]::new($payload.Length)
    for ($row = 0; $row -lt $Height; $row++) {
        $rowOffset = $row * $scanlineLength
        if ($decodedScanlines[$rowOffset] -ne 0) { throw 'Generated carrier uses an unsupported PNG filter.' }
        $rowPayloadOffset = $row * $Width * 3
        for ($column = 0; $column -lt $Width; $column++) {
            $payloadOffset = $rowPayloadOffset + ($column * 3)
            if ($payloadOffset -ge $reconstructed.Length) { break }
            $pixelOffset = $rowOffset + 1 + ($column * 3)
            for ($channel = 0; $channel -lt 3 -and $payloadOffset + $channel -lt $reconstructed.Length; $channel++) {
                $reconstructed[$payloadOffset + $channel] = $decodedScanlines[$pixelOffset + $channel]
            }
        }
    }

    $reconstructedPath = Join-Path ([IO.Path]::GetTempPath()) ('connectome-carrier-' + [guid]::NewGuid().ToString('N') + '.bin')
    try {
        [IO.File]::WriteAllBytes($reconstructedPath, $reconstructed)
        $reconstructedHash = (Get-FileHash -LiteralPath $reconstructedPath -Algorithm SHA256).Hash
    } finally {
        if (Test-Path -LiteralPath $reconstructedPath) {
            Remove-Item -LiteralPath $reconstructedPath -Force
        }
    }

    if ($reconstructedHash -ne $sourceHash) {
        throw "Carrier round-trip hash mismatch. Expected $sourceHash, got $reconstructedHash."
    }

    $outputHash = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
    Write-Host "Compiled $($payload.Length) payload bytes into $Width x $Height RGB PNG."
    Write-Host "Input SHA-256:  $sourceHash"
    Write-Host "Output SHA-256: $outputHash"
    Write-Host 'Carrier round-trip verification passed.'
}
