param(
    [string]$ServerInstance = '.\SQLEXPRESS',
    [string]$Database = 'DatSanThongMinh'
)
$ErrorActionPreference = 'Stop'
$appRoot = Split-Path -Parent $PSScriptRoot
$configPath = Join-Path $appRoot 'backend/appsettings.Local.json'
$origins = @('http://localhost:8081', 'http://127.0.0.1:8081')
$localAddresses = [System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName()) |
    Where-Object { $_.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork -and -not [System.Net.IPAddress]::IsLoopback($_) }
foreach ($address in $localAddresses) { $origins += "http://$($address.IPAddressToString):8081" }

if (Test-Path -LiteralPath $configPath) {
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    $config.Cors.AllowedOrigins = @($config.Cors.AllowedOrigins + $origins | Select-Object -Unique)
    Write-Output 'Giu cau hinh database va khoa JWT local da co; cap nhat CORS.'
} else {
    $connection = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $connection['Data Source'] = $ServerInstance
    $connection['Initial Catalog'] = $Database
    $connection['Integrated Security'] = $true
    $connection['Encrypt'] = $true
    $connection['TrustServerCertificate'] = $true
    $connection['Connect Timeout'] = 5
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $keyBytes = New-Object byte[] 48
    try { $random.GetBytes($keyBytes) } finally { $random.Dispose() }
    $config = @{
        ConnectionStrings = @{ DefaultConnection = $connection.ConnectionString }
        Jwt = @{ Key = [Convert]::ToBase64String($keyBytes) }
        Cors = @{ AllowedOrigins = @($origins | Select-Object -Unique) }
    }
    Write-Output 'Da tao cau hinh local: Windows Authentication va khoa JWT ngau nhien.'
}
$encoding = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json -Depth 6), $encoding)
$envPath = Join-Path $appRoot 'frontend/.env'
if (-not (Test-Path -LiteralPath $envPath)) {
    Copy-Item -LiteralPath (Join-Path $appRoot 'frontend/.env.example') -Destination $envPath
}
Write-Output "Backend: http://localhost:5100/api — Database: $Database"
Write-Output 'Frontend: http://localhost:8081'
Write-Output 'Script khong tao bang, khong sua du lieu, khong thay doi dich vu SQL Server.'
