param([string]$ApiUrl = 'http://localhost:5100/api')
$ErrorActionPreference = 'Stop'
# Read-only checks for the owner venue API. Every write below is rejected by validation, so no rows are created.

function Get-Token([string]$Email) {
    $body = @{ email = $Email; password = '123456' } | ConvertTo-Json
    (Invoke-RestMethod "$ApiUrl/auth/login" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 15).data.accessToken
}
function Get-Status([string]$Method, [string]$Path, [hashtable]$Headers, $Body = $null) {
    $parameters = @{ Uri = "$ApiUrl$Path"; Method = $Method; Headers = $Headers; UseBasicParsing = $true; TimeoutSec = 15 }
    if ($null -ne $Body) { $parameters.Body = ($Body | ConvertTo-Json -Depth 5 -Compress); $parameters.ContentType = 'application/json; charset=utf-8' }
    try { [int](Invoke-WebRequest @parameters).StatusCode }
    catch { if ($null -eq $_.Exception.Response) { throw }; [int]$_.Exception.Response.StatusCode }
}

$owner1 = @{ Authorization = "Bearer $(Get-Token 'owner1@datsan.vn')" }
$owner2 = @{ Authorization = "Bearer $(Get-Token 'owner2@datsan.vn')" }
$player = @{ Authorization = "Bearer $(Get-Token 'an@datsan.vn')" }

$list = (Invoke-RestMethod "$ApiUrl/owner/venues" -Headers $owner1 -TimeoutSec 15).data
if ($list.Count -lt 1) { throw 'owner1 should own demo venues' }
$first = $list[0]
Write-Output "PASS: owner1 lists $($list.Count) venues ($(@($list | ForEach-Object { "$($_.name) [$($_.status)]" }) -join ', '))"

$detail = (Invoke-RestMethod "$ApiUrl/owner/venues/$($first.id)" -Headers $owner1 -TimeoutSec 15).data
if ($detail.courts.Count -lt 1 -or $detail.prices.Count -lt 1) { throw 'Venue detail must include courts and prices' }
Write-Output "PASS: detail of $($detail.name) -> $($detail.courts.Count) courts, $($detail.prices.Count) price rows"

$invalid = @{ name = 'A'; address = 'x'; lat = 0; lng = 0; openTime = '06:00'; closeTime = '22:00'; courts = @(); prices = @() }
foreach ($case in @(
    @{ Name = 'other owner cannot read';   Method = 'Get';  Path = "/owner/venues/$($first.id)"; Headers = $owner2; Body = $null; Status = 404 },
    @{ Name = 'other owner cannot update'; Method = 'Put';  Path = "/owner/venues/$($first.id)"; Headers = $owner2; Body = $invalid; Status = 404 },
    @{ Name = 'player is forbidden';       Method = 'Get';  Path = '/owner/venues'; Headers = $player; Body = $null; Status = 403 },
    @{ Name = 'no token';                  Method = 'Get';  Path = '/owner/venues'; Headers = @{}; Body = $null; Status = 401 },
    @{ Name = 'invalid create rejected';   Method = 'Post'; Path = '/owner/venues'; Headers = $owner1; Body = $invalid; Status = 400 },
    @{ Name = 'invalid update rejected';   Method = 'Put';  Path = "/owner/venues/$($first.id)"; Headers = $owner1; Body = $invalid; Status = 400 })) {
    $status = Get-Status $case.Method $case.Path $case.Headers $case.Body
    if ($status -ne $case.Status) { throw "$($case.Name): expected $($case.Status), got $status" }
    Write-Output "PASS: $($case.Name) -> $status"
}
$after = (Invoke-RestMethod "$ApiUrl/owner/venues" -Headers $owner1 -TimeoutSec 15).data
if ($after.Count -ne $list.Count) { throw 'Venue count changed: a rejected request must not create rows' }
Write-Output 'All owner venue checks passed; no database rows were modified.'
