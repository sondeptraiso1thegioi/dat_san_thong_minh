param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../docs/diagrams')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($outputPath) | Out-Null

# All exports share the same hierarchy. The diagram describes the planned scope.
$groups = @(
    @{
        Id = '1'; Title = 'Quản lý tài khoản'; Actor = 'CHỨC NĂNG CHUNG'; Color = '#526677'
        Items = @(
            @('Đăng ký tài khoản', 'Dành cho người chơi và chủ sân'),
            @('Đăng nhập, đăng xuất', 'Truy cập theo vai trò tài khoản'),
            @('Quản lý hồ sơ cá nhân', 'Thông tin liên hệ và địa chỉ'),
            @('Cập nhật vị trí', 'Ghim vị trí trên bản đồ')
        )
    },
    @{
        Id = '2'; Title = 'Tìm và đặt sân'; Actor = 'NGƯỜI CHƠI'; Color = '#226851'
        Items = @(
            @('Tìm kiếm sân', 'Theo môn, vị trí và khoảng cách'),
            @('Xem thông tin sân', 'Chi tiết, giá, tiện ích, sân VIP'),
            @('Nhận gợi ý sân thông minh', 'Theo nhu cầu, thời tiết, lịch sử'),
            @('Tra cứu và đặt sân', 'Chọn sân, ngày, giờ trống; giữ chỗ'),
            @('Thanh toán đặt sân', 'Thanh toán QR giả lập như Cinema'),
            @('Quản lý lịch đặt sân', 'Xem lịch sử và hủy yêu cầu'),
            @('Xem bản đồ, chỉ đường', 'Mở chỉ đường bằng Google Maps')
        )
    },
    @{
        Id = '3'; Title = 'Quản lý kinh doanh sân'; Actor = 'CHỦ SÂN'; Color = '#936229'
        Items = @(
            @('Đăng ký, cập nhật cụm sân', 'Ảnh, vị trí, tiện ích, giờ mở cửa'),
            @('Quản lý sân con', 'Môn thể thao, mái che, trạng thái'),
            @('Thiết lập bảng giá', 'Theo khung giờ và ngày trong tuần'),
            @('Quản lý đơn đặt sân', 'Xem, xác nhận và hủy đơn'),
            @('Theo dõi doanh thu', 'Tổng thu, hoa hồng và thực nhận'),
            @('Mua và quản lý VIP', 'Thanh toán giả lập, thời hạn gói')
        )
    },
    @{
        Id = '4'; Title = 'Quản trị hệ thống'; Actor = 'QUẢN TRỊ VIÊN'; Color = '#5F527F'
        Items = @(
            @('Kiểm duyệt, quản lý sân', 'Duyệt, từ chối, chỉnh sửa, ẩn sân'),
            @('Quản lý người dùng', 'Xem và khóa tài khoản'),
            @('Quản lý gói VIP', 'Giá, thời hạn và trạng thái'),
            @('Thống kê hoạt động', 'Lưu lượng theo giờ, ngày và sân'),
            @('Đối soát và hoàn tiền', 'Trả chủ sân, hoàn tiền giả lập'),
            @('Cấu hình hệ thống', 'Trọng số AI, thời tiết, hoa hồng')
        )
    }
)

$width = 2560
$height = 1590
$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$graphics.Clear([System.Drawing.Color]::White)
$svg = New-Object System.Text.StringBuilder
[void]$svg.AppendLine('<svg xmlns="http://www.w3.org/2000/svg" width="2560" height="1590" viewBox="0 0 2560 1590" role="img" aria-labelledby="title description">')
[void]$svg.AppendLine('<title id="title">Biểu đồ phân rã chức năng hệ thống đặt sân thể thao thông minh</title>')
[void]$svg.AppendLine('<desc id="description">Cây phân rã gồm bốn nhóm: quản lý tài khoản, tìm và đặt sân, quản lý kinh doanh sân và quản trị hệ thống. Đây là phạm vi dự kiến của toàn bộ dự án.</desc>')
[void]$svg.AppendLine('<rect width="2560" height="1590" fill="white"/>')

function Add-Box {
    param([float]$X, [float]$Y, [float]$W, [float]$H, [string]$Fill, [string]$Stroke = '', [float]$LineWidth = 2)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($Fill))
    $graphics.FillRectangle($brush, $X, $Y, $W, $H)
    $brush.Dispose()
    if ($Stroke) {
        $pen = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml($Stroke), $LineWidth)
        $graphics.DrawRectangle($pen, $X, $Y, $W, $H)
        $pen.Dispose()
    }
    $strokeAttribute = if ($Stroke) { " stroke=`"$Stroke`" stroke-width=`"$LineWidth`"" } else { '' }
    [void]$svg.AppendLine("<rect x=`"$X`" y=`"$Y`" width=`"$W`" height=`"$H`" fill=`"$Fill`"$strokeAttribute/>")
}

function Add-Line {
    param([float]$X1, [float]$Y1, [float]$X2, [float]$Y2, [string]$Color = '#A6B2B9', [float]$LineWidth = 3)
    $pen = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml($Color), $LineWidth)
    $graphics.DrawLine($pen, $X1, $Y1, $X2, $Y2)
    $pen.Dispose()
    [void]$svg.AppendLine("<line x1=`"$X1`" y1=`"$Y1`" x2=`"$X2`" y2=`"$Y2`" stroke=`"$Color`" stroke-width=`"$LineWidth`"/>")
}

function Add-Text {
    param([string]$Value, [float]$X, [float]$Y, [float]$Size, [string]$Color = '#1C3235', [switch]$Bold, [switch]$Centered)
    $style = if ($Bold) { [System.Drawing.FontStyle]::Bold } else { [System.Drawing.FontStyle]::Regular }
    $font = New-Object System.Drawing.Font('Segoe UI', $Size, $style, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($Color))
    $format = New-Object System.Drawing.StringFormat
    $format.FormatFlags = [System.Drawing.StringFormatFlags]::NoWrap
    if ($Centered) { $format.Alignment = [System.Drawing.StringAlignment]::Center }
    $graphics.DrawString($Value, $font, $brush, (New-Object System.Drawing.PointF($X, $Y)), $format)
    $anchor = if ($Centered) { 'middle' } else { 'start' }
    $weight = if ($Bold) { '700' } else { '400' }
    $escaped = [System.Security.SecurityElement]::Escape($Value)
    # Segoe UI's ascent is about 1.075 times the em size; align SVG to DrawString.
    $baseline = $Y + $Size * 1.075
    [void]$svg.AppendLine("<text x=`"$X`" y=`"$baseline`" font-family=`"Segoe UI, Arial, sans-serif`" font-size=`"$Size`" font-weight=`"$weight`" text-anchor=`"$anchor`" fill=`"$Color`">$escaped</text>")
    $format.Dispose()
    $brush.Dispose()
    $font.Dispose()
}

Add-Text 'BIỂU ĐỒ PHÂN RÃ CHỨC NĂNG' 1280 38 43 '#163E35' -Bold -Centered
Add-Text 'Tennis  ·  Pickleball  ·  Cầu lông  ·  Bóng bàn' 1280 102 25 '#617176' -Centered
Add-Box 710 170 1140 108 '#163E35'
Add-Text '0. HỆ THỐNG ĐẶT SÂN THỂ THAO THÔNG MINH' 1280 198 33 '#FFFFFF' -Bold -Centered

# Orthogonal connectors denote decomposition; there are no process arrows.
Add-Line 1280 278 1280 320
Add-Line 362 320 2198 320

$columnLeft = 74
$columnWidth = 576
$columnGap = 36
$headingY = 362
$headingHeight = 104
$leafStartY = 514
$leafHeight = 108
$leafGap = 23

for ($g = 0; $g -lt $groups.Count; $g++) {
    $group = $groups[$g]
    $x = $columnLeft + $g * ($columnWidth + $columnGap)
    $center = $x + $columnWidth / 2
    Add-Line $center 320 $center $headingY
    Add-Box $x $headingY $columnWidth $headingHeight $group.Color
    Add-Text "$($group.Id). $($group.Title)" $center ($headingY + 14) 30 '#FFFFFF' -Bold -Centered
    Add-Text $group.Actor $center ($headingY + 62) 20 '#FFFFFF' -Centered

    $trunkX = $x + 18
    $leafX = $x + 46
    $leafWidth = $columnWidth - 46
    $lastCenterY = $leafStartY + ($group.Items.Count - 1) * ($leafHeight + $leafGap) + $leafHeight / 2
    Add-Line $center ($headingY + $headingHeight) $center ($headingY + $headingHeight + 20) $group.Color 2
    Add-Line $center ($headingY + $headingHeight + 20) $trunkX ($headingY + $headingHeight + 20) $group.Color 2
    Add-Line $trunkX ($headingY + $headingHeight + 20) $trunkX $lastCenterY $group.Color 2

    for ($i = 0; $i -lt $group.Items.Count; $i++) {
        $y = $leafStartY + $i * ($leafHeight + $leafGap)
        $item = $group.Items[$i]
        Add-Line $trunkX ($y + $leafHeight / 2) $leafX ($y + $leafHeight / 2) $group.Color 2
        Add-Box $leafX $y $leafWidth $leafHeight '#FBFCFC' '#D6DFE1' 2
        Add-Box $leafX $y 5 $leafHeight $group.Color
        Add-Text "$($group.Id).$($i + 1)" ($leafX + 18) ($y + 15) 26 $group.Color -Bold
        Add-Text $item[0] ($leafX + 78) ($y + 15) 25 '#213738' -Bold
        Add-Text $item[1] ($leafX + 19) ($y + 61) 23 '#5D7074'
    }
}

Add-Line 74 1490 2486 1490 '#DCE4E2' 2
Add-Text 'PHẠM VI DỰ KIẾN TOÀN DỰ ÁN' 74 1510 22 '#516869' -Bold
Add-Text 'Đường nối thể hiện quan hệ phân rã chức năng; không biểu diễn trình tự xử lý.' 74 1546 22 '#64777A'
Add-Text 'Thanh toán và hoàn tiền: giả lập theo The Sun Cinema.' 2050 1510 22 '#516869' -Centered

[void]$svg.AppendLine('</svg>')
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $outputPath 'phan-ra-chuc-nang.svg'), $svg.ToString(), $utf8)
$bitmap.SetResolution(192, 192)
$bitmap.Save((Join-Path $outputPath 'phan-ra-chuc-nang.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

$mermaid = New-Object System.Text.StringBuilder
[void]$mermaid.AppendLine('flowchart TB')
[void]$mermaid.AppendLine('    root["0. HỆ THỐNG ĐẶT SÂN THỂ THAO THÔNG MINH"]')
foreach ($group in $groups) {
    $groupId = "f$($group.Id)"
    [void]$mermaid.AppendLine("    $groupId[`"$($group.Id). $($group.Title)<br/>$($group.Actor)`"]")
    [void]$mermaid.AppendLine("    root --- $groupId")
    for ($i = 0; $i -lt $group.Items.Count; $i++) {
        $item = $group.Items[$i]
        $itemId = "$($groupId)_$($i + 1)"
        [void]$mermaid.AppendLine("    $itemId[`"$($group.Id).$($i + 1) $($item[0])<br/>$($item[1])`"]")
        [void]$mermaid.AppendLine("    $groupId --- $itemId")
    }
    [void]$mermaid.AppendLine("    style $groupId fill:$($group.Color),color:#fff,stroke:$($group.Color)")
}
[void]$mermaid.AppendLine('    style root fill:#163E35,color:#fff,stroke:#163E35')
[System.IO.File]::WriteAllText((Join-Path $outputPath 'phan-ra-chuc-nang.mmd'), $mermaid.ToString(), $utf8)
Write-Output "Exported SVG, PNG and Mermaid to $outputPath"
