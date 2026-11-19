param([string]$ApiUrl = 'http://localhost:5100/api')
$ErrorActionPreference = 'Stop'

function Assert-Status([string]$Path, [string]$Method, $Body, [int]$ExpectedStatus, [hashtable]$Headers = @{}) {
    $parameters = @{ Uri = "$ApiUrl$Path"; Method = $Method; Headers = $Headers; UseBasicParsing = $true; TimeoutSec = 15 }
    if ($null -ne $Body) { $parameters.Body = ($Body | ConvertTo-Json -Compress); $parameters.ContentType = 'application/json' }
    try {
        $response = Invoke-WebRequest @parameters
        $status = [int]$response.StatusCode
    } catch {
        if ($null -eq $_.Exception.Response) { throw }
        $status = [int]$_.Exception.Response.StatusCode
    }
    if ($status -ne $ExpectedStatus) { throw "$Method $Path expected $ExpectedStatus, got $status" }
    Write-Output "PASS: $Method $Path -> $status"
}

$health = Invoke-RestMethod "$ApiUrl/health" -TimeoutSec 15
if ($health.status -ne 'ready') { throw 'API/database not ready' }
Write-Output 'PASS: API reads the existing SQL Server users table'

$accounts = @(
    @{ Email = 'an@datsan.vn'; Role = 'player' },
    @{ Email = 'owner1@datsan.vn'; Role = 'owner' },
    @{ Email = 'admin@datsan.vn'; Role = 'admin' }
)
foreach ($account in $accounts) {
    # These credentials are from the existing SQL demo data. Never print tokens.
    $body = @{ email = $account.Email; password = '123456'; role = 'admin' } | ConvertTo-Json
    $login = Invoke-RestMethod "$ApiUrl/auth/login" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 15
    if ($login.data.user.role -ne $account.Role) { throw 'Role must come from the database, not the submitted role' }
    $headers = @{ Authorization = "Bearer $($login.data.accessToken)" }
    $profile = Invoke-RestMethod "$ApiUrl/me" -Headers $headers -TimeoutSec 15
    if ($profile.data.email -ne $account.Email -or $profile.data.role -ne $account.Role) { throw 'Profile mismatch' }
    if ($profile.data.id -isnot [string]) { throw 'User ID must be returned as a string' }
    if ($profile.data.PSObject.Properties.Name -contains 'passwordHash') { throw 'Password hash leaked' }
    Write-Output "PASS: real database login + authenticated profile for $($account.Role)"
}
Assert-Status '/auth/login' 'Post' @{ email = 'an@datsan.vn'; password = 'wrong-password' } 401
Assert-Status '/auth/login' 'Post' @{ email = 'not-a-user@datsan.vn'; password = 'wrong-password' } 401
Assert-Status '/auth/login' 'Post' @{ email = 'not-an-email'; password = '' } 400
Assert-Status '/me' 'Get' $null 401
Assert-Status '/me' 'Get' $null 401 @{ Authorization = 'Bearer invalid.token.value' }
# Registration checks that are all rejected before insert, so the database stays unchanged.
Assert-Status '/auth/register' 'Post' @{ name = 'Kiem Tra'; phone = '0999999990'; email = 'verify-admin@datsan.vn'; password = '123456'; role = 'admin' } 400
Assert-Status '/auth/register' 'Post' @{ name = 'Kiem Tra'; phone = '12345'; email = 'verify-phone@datsan.vn'; password = '123456'; role = 'player' } 400
Assert-Status '/auth/register' 'Post' @{ name = 'Kiem Tra'; phone = '0999999991'; email = 'verify-short@datsan.vn'; password = '123'; role = 'player' } 400
Assert-Status '/auth/register' 'Post' @{ name = 'Kiem Tra'; phone = '0999999992'; email = 'AN@datsan.vn'; password = '123456'; role = 'player' } 409
Assert-Status '/auth/register' 'Post' @{ name = 'Kiem Tra'; phone = '0900000004'; email = 'verify-phone-taken@datsan.vn'; password = '123456'; role = 'owner' } 409
Write-Output 'All live authentication checks passed; no database rows were modified.'
