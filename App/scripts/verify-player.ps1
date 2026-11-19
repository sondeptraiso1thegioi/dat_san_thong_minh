param([string]$ApiUrl = 'http://localhost:5100/api')
$ErrorActionPreference = 'Stop'
# Read-only checks for the player home API against the existing SQL demo data. Never prints tokens.

function Get-Token([string]$Email) {
    $body = @{ email = $Email; password = '123456' } | ConvertTo-Json
    (Invoke-RestMethod "$ApiUrl/auth/login" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 15).data.accessToken
}
function Get-Status([string]$Path, [hashtable]$Headers = @{}) {
    try { [int](Invoke-WebRequest "$ApiUrl$Path" -Headers $Headers -UseBasicParsing -TimeoutSec 15).StatusCode }
    catch { if ($null -eq $_.Exception.Response) { throw }; [int]$_.Exception.Response.StatusCode }
}

$player = @{ Authorization = "Bearer $(Get-Token 'an@datsan.vn')" }
$data = (Invoke-RestMethod "$ApiUrl/player/home" -Headers $player -TimeoutSec 15).data
if ($data.location.source -ne 'profile') { throw "Expected profile location, got $($data.location.source)" }
if ($data.nearby.Count -lt 1) { throw 'Expected approved venues in "Sân gần bạn"' }
$distances = @($data.nearby | ForEach-Object { [double]$_.distanceKm })
for ($i = 1; $i -lt $distances.Count; $i++) { if ($distances[$i] -lt $distances[$i - 1]) { throw 'Nearby venues are not sorted by distance' } }
if (@($data.nearby | Where-Object { $_.name -eq 'Cụm sân D' }).Count -gt 0) { throw 'Pending venue must not be shown' }
Write-Output "PASS: home for player -> $($data.nearby.Count) venues, nearest $($data.nearby[0].name) ($($data.nearby[0].distanceKm) km), $($data.featured.Count) featured"
$featuredOutside = @($data.featured | Where-Object { [double]$_.distanceKm -gt [double]$data.radiusKm })
if ($featuredOutside.Count -gt 0) { throw 'Featured VIP venue outside default radius' }
Write-Output 'PASS: featured venues are VIP within the default radius'

$deviceData = (Invoke-RestMethod "$ApiUrl/player/home?lat=21.07&lng=105.82" -Headers $player -TimeoutSec 15).data
if ($deviceData.location.source -ne 'device') { throw 'Device location must override the profile location' }
Write-Output "PASS: device location -> nearest $($deviceData.nearby[0].name)"

foreach ($case in @(@{ Path = '/player/home'; Headers = @{}; Status = 401 },
                    @{ Path = '/player/home?lat=200&lng=105'; Headers = $player; Status = 400 },
                    @{ Path = '/player/home?lat=21'; Headers = $player; Status = 400 },
                    @{ Path = '/player/home'; Headers = @{ Authorization = "Bearer $(Get-Token 'owner1@datsan.vn')" }; Status = 403 })) {
    $status = Get-Status $case.Path $case.Headers
    if ($status -ne $case.Status) { throw "GET $($case.Path) expected $($case.Status), got $status" }
    Write-Output "PASS: GET $($case.Path) -> $status"
}
# ---- Tìm sân (GET /player/venues) ----
$search = (Invoke-RestMethod "$ApiUrl/player/venues?sport=badminton" -Headers $player -TimeoutSec 15).data
$ids = @($search.venues | ForEach-Object { $_.venue.name })
if ($ids -contains 'Cụm sân A') { throw 'Cụm sân A has no badminton court and must be filtered out' }
if ($search.venues.Count -lt 1) { throw 'Expected badminton venues' }
Write-Output "PASS: search badminton -> $($ids -join ', ')"

$unaccented = (Invoke-RestMethod "$ApiUrl/player/venues?q=tay%20ho" -Headers $player -TimeoutSec 15).data
if (@($unaccented.venues | Where-Object { $_.venue.name -eq 'Cụm sân C' }).Count -ne 1) { throw 'Search "tay ho" must find Cụm sân C (Tây Hồ)' }
Write-Output 'PASS: search without Vietnamese accents'

$tomorrow = [DateTime]::UtcNow.AddHours(7).AddDays(1).ToString('yyyy-MM-dd')
$slot = (Invoke-RestMethod "$ApiUrl/player/venues?sport=tennis&date=$tomorrow&startHour=16&hours=2" -Headers $player -TimeoutSec 15).data
$a = @($slot.venues | Where-Object { $_.venue.name -eq 'Cụm sân A' })[0]
if ($null -eq $a) { throw 'Cụm sân A should have free tennis courts tomorrow 16:00 (demo data has no booking then)' }
if ([long]$a.slotPrice -ne 270000) { throw "Expected 120.000 + 150.000 = 270.000 for 16:00-18:00 at Cụm sân A, got $($a.slotPrice)" }
Write-Output "PASS: slot search tomorrow 16:00-18:00 -> Cụm sân A $($a.freeCourts) free courts, $($a.slotPrice) VND"

$past = [DateTime]::UtcNow.AddHours(7).AddDays(-1).ToString('yyyy-MM-dd')
foreach ($case in @(@{ Path = "/player/venues?date=$past&startHour=10"; Code = 400 },
                    @{ Path = '/player/venues?sport=golf'; Code = 400 },
                    @{ Path = '/player/venues?radiusKm=100'; Code = 400 })) {
    $status = Get-Status $case.Path $player
    if ($status -ne $case.Code) { throw "GET $($case.Path) expected $($case.Code), got $status" }
    Write-Output "PASS: GET $($case.Path) -> $status"
}
# ---- Chi tiết cụm sân + lưới giờ (GET /player/venues/{id}), chỉ đọc ----
$detail = (Invoke-RestMethod "$ApiUrl/player/venues/$($a.venue.id)?date=$tomorrow" -Headers $player -TimeoutSec 15).data
if ($detail.date -ne $tomorrow) { throw "Venue detail should be for $tomorrow, got $($detail.date)" }
if ([int]$detail.maxHours -ne 3) { throw "maxHours must be 3, got $($detail.maxHours)" }
$tennis = @($detail.courts | Where-Object { $_.sport -eq 'tennis' })
if ($tennis.Count -lt 1) { throw 'Cụm sân A should list its tennis courts' }
$cell = @($tennis[0].slots | Where-Object { $_.hour -eq 16 })[0]
if ($null -eq $cell -or [long]$cell.price -ne 120000) { throw "Tennis 16:00 tomorrow should cost 120.000, got $($cell.price)" }
Write-Output "PASS: venue detail -> $($detail.courts.Count) courts, hours $($detail.hours[0])..$($detail.hours[-1]), hold $($detail.holdMinutes) min"

foreach ($case in @(@{ Path = '/player/venues/999999'; Code = 404 },
                    @{ Path = "/player/venues/$($a.venue.id)?date=$past"; Code = 400 },
                    @{ Path = "/player/venues?sport=tennis&date=$tomorrow&startHour=16&hours=4"; Code = 400 },
                    @{ Path = '/player/bookings/999999'; Code = 404 })) {
    $status = Get-Status $case.Path $player
    if ($status -ne $case.Code) { throw "GET $($case.Path) expected $($case.Code), got $status" }
    Write-Output "PASS: GET $($case.Path) -> $status"
}
Write-Output 'All player home and search checks passed; no database rows were modified.'
