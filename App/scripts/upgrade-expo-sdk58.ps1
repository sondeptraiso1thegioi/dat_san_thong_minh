$ErrorActionPreference = "Stop"

$frontend = Join-Path $PSScriptRoot "..\frontend"
$frontend = [System.IO.Path]::GetFullPath($frontend)
Push-Location $frontend
try {
    $nodeVersionText = (& node --version).Trim().TrimStart('v')
    if (-not $nodeVersionText) { throw "Không tìm thấy Node.js. Cài Node.js 22.13.x trở lên rồi chạy lại." }
    $nodeVersion = [version]$nodeVersionText
    if ($nodeVersion -lt [version]"22.13.0") {
        throw "SDK 58 yêu cầu Node.js >= 22.13.0. Phiên bản hiện tại: v$nodeVersionText"
    }

    if (Test-Path "package-lock.json") {
        Remove-Item "package-lock.json" -Force
        Write-Host "Đã xóa package-lock cũ để tái tạo lockfile cho SDK 58." -ForegroundColor Yellow
    }

    Write-Host "Cài dependencies..." -ForegroundColor Cyan
    npm install
    if ($LASTEXITCODE -ne 0) { throw "npm install thất bại (exit $LASTEXITCODE)." }

    Write-Host "Đồng bộ package theo Expo SDK 58..." -ForegroundColor Cyan
    npx expo install --fix
    if ($LASTEXITCODE -ne 0) { throw "expo install --fix thất bại (exit $LASTEXITCODE)." }

    Write-Host "Kiểm tra Expo dependencies..." -ForegroundColor Cyan
    npx expo-doctor
    if ($LASTEXITCODE -ne 0) { throw "expo-doctor báo lỗi (exit $LASTEXITCODE). Hãy xem chi tiết ở trên." }

    Write-Host "Kiểm tra TypeScript..." -ForegroundColor Cyan
    npm run typecheck
    if ($LASTEXITCODE -ne 0) { throw "TypeScript typecheck thất bại (exit $LASTEXITCODE)." }

    Write-Host "Nâng cấp Expo SDK 58 hoàn tất ở mức cài dependency và kiểm tra tĩnh." -ForegroundColor Green
}
finally {
    Pop-Location
}
