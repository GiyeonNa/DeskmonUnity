# 앱 아이콘 PNG -> 멀티사이즈 .ico
#
# 왜 필요한가: Unity는 PlayerSettings 아이콘으로 exe 아이콘을 알아서 박지만,
# 인스톨러(Inno Setup)의 SetupIconFile은 .ico 형식만 받는다.
# 256/48/32/16 네 크기를 PNG 엔트리로 담는다 (Vista+ 표준, 작업표시줄~탐색기 대응).
#
# 사용: powershell -ExecutionPolicy Bypass -File Tools\MakeIcon.ps1
# 원본이 바뀌면 다시 실행한다.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$srcPath = Join-Path $root 'Assets\AppIcon\deskmon_app_icon_1024.png'
$outPath = Join-Path $root 'Tools\Installer\deskmon.ico'

if (-not (Test-Path $srcPath)) { throw "원본 없음: $srcPath" }

$src = [System.Drawing.Image]::FromFile($srcPath)
$sizes = @(256, 48, 32, 16)
$entries = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    # 원본이 1024 일러스트라 고품질 보간이 맞다 (순수 도트였다면 NearestNeighbor)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($src, 0, 0, $s, $s)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $entries += , @($s, $ms.ToArray())
    $ms.Dispose()
}
$src.Dispose()

# 주의: 이 파일은 반드시 UTF-8 BOM으로 저장해야 한다. BOM이 없으면 PS 5.1이
# CP949로 읽어 한글 주석의 바이트가 뒷줄 코드를 삼킨다 - 실제로 헤더 필드가
# 조용히 누락된 채 생성됐다. 그래서 끝에 자가 검증을 둔다.
$buf = New-Object 'System.Collections.Generic.List[byte]'
function Add16([int]$v) { $script:buf.AddRange([System.BitConverter]::GetBytes([uint16]$v)) }
function Add32([int]$v) { $script:buf.AddRange([System.BitConverter]::GetBytes([uint32]$v)) }

# ICONDIR
Add16 0                                # 예약
Add16 1                                # 타입 1 = 아이콘
Add16 $entries.Count

# ICONDIRENTRY 목록 (256은 0으로 표기하는 것이 규격)
$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    $s = $e[0]; $data = $e[1]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $buf.Add([byte]$dim)               # 너비
    $buf.Add([byte]$dim)               # 높이
    $buf.Add([byte]0)                  # 팔레트 수 (트루컬러 = 0)
    $buf.Add([byte]0)                  # 예약
    Add16 1                            # 플레인
    Add16 32                           # 비트 수
    Add32 $data.Length
    Add32 $offset
    $offset += $data.Length
}
foreach ($e in $entries) { $buf.AddRange([byte[]]$e[1]) }

[System.IO.File]::WriteAllBytes($outPath, $buf.ToArray())

# ── 자가 검증 - 깨진 ico가 조용히 커밋되는 것을 막는다 ──
$b = [System.IO.File]::ReadAllBytes($outPath)
$count = [System.BitConverter]::ToUInt16($b, 4)
$bits  = [System.BitConverter]::ToUInt16($b, 12)
$size1 = [System.BitConverter]::ToUInt32($b, 14)
$off1  = [System.BitConverter]::ToUInt32($b, 18)
if ($count -ne $entries.Count) { throw "검증 실패: 엔트리 수 $count (기대 $($entries.Count))" }
if ($bits -ne 32)              { throw "검증 실패: 비트 수 $bits (기대 32)" }
if ($off1 -ne 6 + 16 * $entries.Count) { throw "검증 실패: 첫 오프셋 $off1" }
# 첫 엔트리 데이터가 PNG 시그니처인지
if ($b[$off1 + 1] -ne 0x50 -or $b[$off1 + 2] -ne 0x4E -or $b[$off1 + 3] -ne 0x47) {
    throw "검증 실패: 첫 엔트리가 PNG가 아님"
}

Write-Host "생성: $outPath ($((Get-Item $outPath).Length) bytes, $($entries.Count)개 크기)"
