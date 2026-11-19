param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../docs/diagrams')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($outputPath) | Out-Null
$bitmap = New-Object System.Drawing.Bitmap(2800, 2100)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$graphics.Clear([System.Drawing.Color]::White)
$svg = New-Object System.Text.StringBuilder
[void]$svg.AppendLine('<svg xmlns="http://www.w3.org/2000/svg" width="2800" height="2100" viewBox="0 0 2800 2100" role="img" aria-labelledby="title description">')
[void]$svg.AppendLine('<title id="title">Kiến trúc hệ thống đặt sân thể thao thông minh</title>')
[void]$svg.AppendLine('<desc id="description">Ba vai trò dùng ứng dụng Expo, kết nối backend ASP.NET Core theo tầng, SQL Server, MinIO, PaymentGateway giả lập, bản đồ, Open-Meteo và LLM tùy chọn. Đây là kiến trúc mục tiêu; hiện đã có đăng nhập và hồ sơ phiên kết nối database thật.</desc>')
[void]$svg.AppendLine('<rect width="2800" height="2100" fill="white"/>')

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

function Add-Text {
    param([string]$Value, [float]$X, [float]$Y, [float]$Size = 26, [string]$Color = '#233B40', [switch]$Bold, [switch]$Centered)
    $style = if ($Bold) { [System.Drawing.FontStyle]::Bold } else { [System.Drawing.FontStyle]::Regular }
    $font = New-Object System.Drawing.Font('Segoe UI', $Size, $style, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($Color))
    $format = New-Object System.Drawing.StringFormat
    $format.FormatFlags = [System.Drawing.StringFormatFlags]::NoWrap
    if ($Centered) { $format.Alignment = [System.Drawing.StringAlignment]::Center }
    $graphics.DrawString($Value, $font, $brush, (New-Object System.Drawing.PointF($X, $Y)), $format)
    $escaped = [System.Security.SecurityElement]::Escape($Value)
    $baseline = $Y + $Size * 1.075
    $anchor = if ($Centered) { 'middle' } else { 'start' }
    $weight = if ($Bold) { '700' } else { '400' }
    [void]$svg.AppendLine("<text x=`"$X`" y=`"$baseline`" font-family=`"Segoe UI, Arial, sans-serif`" font-size=`"$Size`" font-weight=`"$weight`" text-anchor=`"$anchor`" fill=`"$Color`">$escaped</text>")
    $font.Dispose()
    $brush.Dispose()
    $format.Dispose()
}

function Add-Path {
    param([float[]]$Points, [string]$Color = '#617681', [switch]$Arrow, [switch]$Dashed)
    $pen = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml($Color), 3)
    if ($Dashed) { $pen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash }
    for ($i = 0; $i -lt $Points.Count - 2; $i += 2) {
        $graphics.DrawLine($pen, $Points[$i], $Points[$i + 1], $Points[$i + 2], $Points[$i + 3])
    }
    $pointPairs = for ($i = 0; $i -lt $Points.Count; $i += 2) { "$($Points[$i]),$($Points[$i + 1])" }
    $dashAttribute = if ($Dashed) { ' stroke-dasharray="12 9"' } else { '' }
    [void]$svg.AppendLine("<polyline points=`"$($pointPairs -join ' ')`" fill=`"none`" stroke=`"$Color`" stroke-width=`"3`"$dashAttribute/>")
    if ($Arrow) {
        $x2 = $Points[-2]; $y2 = $Points[-1]
        $dx = $x2 - $Points[-4]; $dy = $y2 - $Points[-3]
        $length = [Math]::Sqrt($dx * $dx + $dy * $dy)
        $ux = $dx / $length; $uy = $dy / $length
        $ax = $x2 - 15 * $ux + 7 * $uy; $ay = $y2 - 15 * $uy - 7 * $ux
        $bx = $x2 - 15 * $ux - 7 * $uy; $by = $y2 - 15 * $uy + 7 * $ux
        $triangle = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF($x2, $y2)),
            (New-Object System.Drawing.PointF($ax, $ay)),
            (New-Object System.Drawing.PointF($bx, $by))
        )
        $brush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($Color))
        $graphics.FillPolygon($brush, $triangle)
        $brush.Dispose()
        [void]$svg.AppendLine("<polygon points=`"$x2,$y2 $ax,$ay $bx,$by`" fill=`"$Color`"/>")
    }
    $pen.Dispose()
}

Add-Text 'KIẾN TRÚC HỆ THỐNG' 1400 35 46 '#163E35' -Bold -Centered
Add-Text 'Ứng dụng đặt sân thể thao thông minh  ·  Kiến trúc mục tiêu toàn dự án' 1400 103 28 '#667B7D' -Centered

# User entry points.
$actors = @(
    @{ X = 80; Label = 'NGƯỜI CHƠI'; Detail = 'Tìm sân · đặt sân · thanh toán' },
    @{ X = 655; Label = 'CHỦ SÂN'; Detail = 'Quản lý sân · doanh thu · VIP' },
    @{ X = 1230; Label = 'QUẢN TRỊ VIÊN'; Detail = 'Kiểm duyệt · đối soát · cấu hình' }
)
foreach ($actor in $actors) {
    Add-Box $actor.X 175 510 105 '#F2F6F4' '#CDDAD4'
    Add-Text $actor.Label ($actor.X + 255) 190 29 '#235745' -Bold -Centered
    Add-Text $actor.Detail ($actor.X + 255) 236 24 '#5E7470' -Centered
    Add-Path @(($actor.X + 255), 280, ($actor.X + 255), 312)
}
Add-Path @(335, 312, 1485, 312)
Add-Path @(910, 312, 910, 350) -Arrow

# A single Expo codebase serves all three roles and both mobile/web surfaces.
Add-Box 80 350 1660 310 '#F1F7F4' '#8FB5A6' 3
Add-Text 'FRONTEND  ·  React Native + Expo + TypeScript' 910 367 36 '#1D5945' -Bold -Centered
Add-Text 'Một codebase cho mobile và web  ·  App/frontend' 910 419 26 '#55786B' -Centered
$frontendCards = @(
    @{ X = 116; Title = 'Giao diện và điều hướng'; Line1 = 'Màn hình theo vai trò'; Line2 = 'React Navigation' },
    @{ X = 654; Title = 'Trạng thái và gọi API'; Line1 = 'Zustand · Axios'; Line2 = 'Services / Store / API client' },
    @{ X = 1192; Title = 'Phiên đăng nhập và bản đồ'; Line1 = 'SecureStore / sessionStorage'; Line2 = 'react-native-maps / Leaflet' }
)
foreach ($card in $frontendCards) {
    Add-Box $card.X 485 514 145 '#FFFFFF' '#CDDFD6'
    Add-Text $card.Title ($card.X + 257) 500 27 '#234E40' -Bold -Centered
    Add-Text $card.Line1 ($card.X + 257) 547 25 '#58726A' -Centered
    Add-Text $card.Line2 ($card.X + 257) 588 24 '#58726A' -Centered
}

# REST requests and SignalR updates use independent channels.
Add-Path @(1050, 660, 1050, 840) '#2F6486' -Arrow
Add-Text 'HTTP(S) · REST / JSON · JWT' 1084 716 26 '#2F6486'
Add-Path @(675, 840, 675, 660) '#3D8B7D' -Arrow -Dashed
Add-Text 'SignalR: cập nhật lịch trống' 390 731 25 '#3D8B7D' -Centered

# Backend application boundary and its internal layers.
Add-Box 80 840 1660 640 '#F1F5FA' '#8DA9C2' 3
Add-Text 'BACKEND  ·  ASP.NET Core Web API (.NET 8 / C#)' 910 857 35 '#274F70' -Bold -Centered
Add-Text 'REST API · SignalR · Dependency Injection  |  App/backend' 910 911 26 '#607D94' -Centered
Add-Box 116 977 1588 74 '#FFFFFF' '#C5D4E1'
Add-Text 'Controllers · DTOs · JWT Bearer · Phân quyền player / owner / admin' 910 993 28 '#315A7B' -Bold -Centered
Add-Path @(615, 1051, 615, 1085) '#7191AE' -Arrow
Add-Path @(1423, 1051, 1423, 1085) '#7191AE' -Arrow
Add-Box 116 1085 995 182 '#FFFFFF' '#C5D4E1'
Add-Text 'DỊCH VỤ NGHIỆP VỤ' 613 1099 29 '#315A7B' -Bold -Centered
Add-Text 'Tài khoản · cụm sân · sân con · bảng giá · đặt sân' 613 1151 27 '#5B7489' -Centered
Add-Text 'Thanh toán · VIP · doanh thu · quản trị' 613 1202 27 '#5B7489' -Centered
Add-Box 1143 1085 561 182 '#F5F1FA' '#C1AED2'
Add-Text 'GỢI Ý SÂN (C#)' 1423 1099 29 '#665180' -Bold -Centered
Add-Text 'Lọc ứng viên · chấm điểm' 1423 1151 26 '#7A668F' -Centered
Add-Text 'Kết hợp thời tiết · sinh lý do' 1423 1202 26 '#7A668F' -Centered
Add-Path @(615, 1267, 615, 1310) '#7191AE' -Arrow
Add-Path @(1423, 1267, 1423, 1310) '#7191AE' -Arrow
Add-Box 116 1310 1588 90 '#FFFFFF' '#C5D4E1'
Add-Text 'Repositories  →  Entity Framework Core 8  →  AppDbContext' 910 1318 29 '#315A7B' -Bold -Centered
Add-Text 'Truy cập database-first theo schema SQL Server có sẵn' 910 1360 24 '#607D94' -Centered
Add-Text 'Tác vụ nền dự kiến: hết hạn giữ chỗ · hết hạn VIP' 910 1422 25 '#607D94' -Centered

# Data and image storage remain behind the backend.
Add-Path @(610, 1480, 610, 1650) '#46697C' -Arrow
Add-Text 'Dữ liệu nghiệp vụ · EF Core' 650 1547 26 '#46697C'
Add-Path @(1475, 1480, 1475, 1650) '#46697C' -Arrow
Add-Text 'S3 SDK: lưu / đọc ảnh' 1503 1565 24 '#46697C'
Add-Box 80 1650 1060 260 '#F3F6F8' '#A6BAC5' 3
Add-Text 'SQL SERVER  ·  DatSanThongMinh' 610 1663 33 '#315768' -Bold -Centered
Add-Text 'users · venues · courts · price_rules' 610 1720 26 '#5C7580' -Centered
Add-Text 'bookings · payments · vip_plans · vip_subscriptions' 610 1765 25 '#5C7580' -Centered
Add-Text 'weather_cache · suggestion_logs · ai_settings · reports' 610 1810 25 '#5C7580' -Centered
Add-Text 'Ràng buộc · trigger · transaction chống trùng lịch' 610 1864 24 '#315768' -Bold -Centered
Add-Box 1210 1650 530 260 '#F3F6F8' '#A6BAC5' 3
Add-Text 'MINIO' 1475 1680 34 '#315768' -Bold -Centered
Add-Text 'Object Storage · S3 API' 1475 1744 27 '#5C7580' -Centered
Add-Text 'Lưu ảnh cụm sân' 1475 1793 27 '#5C7580' -Centered
Add-Text 'Backend quản lý upload' 1475 1841 25 '#5C7580' -Centered

# External integrations. Paths are future connections, not live API calls.
Add-Box 2160 350 560 250 '#F0F7FA' '#A1BEC9' 3
Add-Text 'BẢN ĐỒ & CHỈ ĐƯỜNG' 2440 372 30 '#3B697C' -Bold -Centered
Add-Text 'OpenStreetMap: bản đồ web' 2440 437 25 '#607E8B' -Centered
Add-Text 'Google Maps: mở link chỉ đường' 2440 482 25 '#607E8B' -Centered
Add-Text 'Kết nối trực tiếp từ ứng dụng' 2440 541 24 '#607E8B' -Centered
Add-Path @(1740, 450, 2160, 450) '#5D8AA0' -Arrow
Add-Text 'Bản đồ / chỉ đường' 1950 410 24 '#5D8AA0' -Centered

Add-Box 2160 840 560 360 '#FFF7EC' '#C9A678' 3
Add-Text 'PAYMENTGATEWAY' 2440 863 32 '#8D6029' -Bold -Centered
Add-Text 'GIẢ LẬP NHƯ CINEMA' 2440 914 28 '#8D6029' -Bold -Centered
Add-Text 'ASP.NET Core .NET 8' 2440 975 26 '#92774F' -Centered
Add-Text 'Tạo bill / QR · nút trả tiền demo' 2440 1022 25 '#92774F' -Centered
Add-Text 'Chữ ký HMAC-SHA256' 2440 1069 26 '#92774F' -Centered
Add-Text 'Đặt sân / mua VIP' 2440 1122 25 '#92774F' -Centered
Add-Path @(1740, 600, 1950, 600, 1950, 760, 2440, 760, 2440, 840) '#B28A55' -Arrow -Dashed
Add-Text 'Nút thanh toán demo' 2300 714 25 '#A47B45' -Centered
Add-Path @(1740, 977, 2160, 977) '#A77D44' -Arrow
Add-Text 'Tạo bill (HMAC)' 1950 934 25 '#A77D44' -Centered
Add-Path @(2160, 1100, 1740, 1100) '#A77D44' -Arrow
Add-Text 'Callback (HMAC)' 1950 1057 25 '#A77D44' -Centered

Add-Box 2160 1280 560 195 '#F5F1FA' '#C1AED2' 3
Add-Text 'OPEN-METEO' 2440 1299 31 '#665180' -Bold -Centered
Add-Text 'Dự báo thời tiết theo giờ' 2440 1362 26 '#7A668F' -Centered
Add-Text 'Cache tại weather_cache' 2440 1410 25 '#7A668F' -Centered
Add-Path @(1704, 1175, 1830, 1175, 1830, 1378, 2160, 1378) '#8D76A7' -Arrow
Add-Text 'Dự báo theo giờ' 1995 1334 24 '#8D76A7' -Centered

Add-Box 2160 1550 560 210 '#F5F1FA' '#C1AED2' 3
Add-Text 'LLM API (TÙY CHỌN)' 2440 1571 31 '#665180' -Bold -Centered
Add-Text 'Gemini hoặc Claude' 2440 1631 27 '#7A668F' -Centered
Add-Text 'Hiểu câu nhập → dữ liệu cấu trúc' 2440 1682 24 '#7A668F' -Centered
Add-Path @(1704, 1230, 1780, 1230, 1780, 1655, 2160, 1655) '#8D76A7' -Arrow -Dashed
Add-Text 'Hiểu câu nhập (tùy chọn)' 1970 1606 23 '#8D76A7' -Centered
Add-Text 'Người dùng tự cấu hình AI / thời tiết' 2440 1814 25 '#7A668F' -Centered
Add-Text 'theo hướng dẫn khi triển khai.' 2440 1853 25 '#7A668F' -Centered

Add-Path @(80, 1970, 2720, 1970) '#D9E2E4'
Add-Text 'HIỆN TẠI: đăng nhập 3 vai trò và hồ sơ phiên → API JWT → SQL Server thật. Các phần còn lại là kiến trúc dự kiến.' 80 1993 26 '#49656D'
Add-Text 'Mũi tên chỉ hướng khởi tạo yêu cầu / gửi thông báo; phản hồi được trả trên cùng kết nối. Nét đứt: kênh bổ trợ hoặc tùy chọn.' 80 2041 24 '#71858B'

[void]$svg.AppendLine('</svg>')
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $outputPath 'kien-truc-he-thong.svg'), $svg.ToString(), $utf8)
$bitmap.SetResolution(192, 192)
$bitmap.Save((Join-Path $outputPath 'kien-truc-he-thong.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

$mermaid = @'
flowchart TB
    player["Người chơi"]
    owner["Chủ sân"]
    admin["Quản trị viên"]

    subgraph frontend["FRONTEND — App/frontend"]
        app["React Native + Expo + TypeScript<br/>Mobile và web · React Navigation"]
        client["Zustand · Axios<br/>SecureStore / sessionStorage"]
        maps["react-native-maps / Leaflet"]
        app --- client
        app --- maps
    end
    player --> app
    owner --> app
    admin --> app

    subgraph backend["BACKEND — ASP.NET Core .NET 8 / C# — App/backend"]
        api["Controllers · DTOs<br/>JWT Bearer · Phân quyền player / owner / admin"]
        services["Services: tài khoản, sân, giá, đặt sân<br/>thanh toán, VIP, doanh thu, quản trị"]
        ai["Recommendation — C#<br/>Lọc ứng viên · chấm điểm · sinh lý do"]
        realtime["SignalR Hub<br/>Làm mới lịch trống"]
        jobs["Tác vụ nền<br/>Hết hạn giữ chỗ / VIP"]
        data["Repositories → EF Core 8 → AppDbContext<br/>Database-first theo schema có sẵn"]
        api --> services
        api --> ai
        services --> data
        ai --> data
        services --> realtime
        jobs --> data
    end

    client -->|"HTTP(S) · REST / JSON · JWT"| api
    realtime -.->|"Cập nhật lịch trống"| app
    sql[("SQL Server · DatSanThongMinh<br/>12 bảng · ràng buộc / trigger / transaction")]
    minio[("MinIO · S3 API<br/>Ảnh cụm sân")]
    data --> sql
    services -->|"S3 SDK"| minio

    gateway["PaymentGateway giả lập như Cinema<br/>ASP.NET Core .NET 8<br/>Tạo bill / QR · đối chiếu bill đã lưu"]
    services -->|"Tạo bill · HMAC-SHA256"| gateway
    app -.->|"Nút thanh toán demo / user-pay"| gateway
    gateway -->|"Callback · HMAC-SHA256"| api
    app -->|"Đọc trạng thái từ backend"| api

    weather["Open-Meteo<br/>Dự báo thời tiết theo giờ"]
    llm["LLM tùy chọn: Gemini hoặc Claude<br/>Hiểu câu nhập → dữ liệu cấu trúc"]
    publicMaps["OpenStreetMap: bản đồ web<br/>Google Maps: mở link chỉ đường"]
    ai -->|"Dự báo; cache trong SQL Server"| weather
    ai -.->|"Hiểu câu nhập nếu được cấu hình"| llm
    maps --> publicMaps

    style frontend fill:#F1F7F4,stroke:#8FB5A6,color:#1D5945
    style backend fill:#F1F5FA,stroke:#8DA9C2,color:#274F70
    style gateway fill:#FFF7EC,stroke:#C9A678,color:#8D6029
    style ai fill:#F5F1FA,stroke:#C1AED2,color:#665180
    style weather fill:#F5F1FA,stroke:#C1AED2,color:#665180
    style llm fill:#F5F1FA,stroke:#C1AED2,color:#665180
'@
[System.IO.File]::WriteAllText((Join-Path $outputPath 'kien-truc-he-thong.mmd'), $mermaid, $utf8)
Write-Output "Exported SVG, PNG and Mermaid to $outputPath"
