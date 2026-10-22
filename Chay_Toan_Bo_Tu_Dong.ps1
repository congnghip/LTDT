# Script tự động hóa toàn bộ: Bật WebAPI, Bật Cloudflare Tunnel, Cập nhật .env, Mở Swagger
$ErrorActionPreference = "SilentlyContinue"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "   HE THONG TU DONG HOA 100% - KHONG CAN THAO TAC THU CONG            " -ForegroundColor Yellow
Write-Host "   Bui Nguyen Cong Nghiep - Smart Attendance Web API                  " -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Kiem tra va khoi chay Backend Web API
Write-Host "[1/4] Kiem tra Web API (Port 5000)..." -ForegroundColor Yellow
$portCheck = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue
if (-not $portCheck) {
    Write-Host "      -> Web API chua bat. Dang tu dong bat Web API..." -ForegroundColor Gray
    Start-Process -FilePath "cmd.exe" -ArgumentList "/c cd /d `"$projectRoot\Sang6_WebAPI`" && dotnet run" -WindowStyle Normal
    $timeout = 20
    while ($timeout -gt 0) {
        Start-Sleep -Seconds 2
        $portCheck = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue
        if ($portCheck) { break }
        $timeout -= 2
    }
}
Write-Host "      [OK] Web API da san sang tren cong 5000!" -ForegroundColor Green

# 2. Khoi chay Cloudflare Tunnel va lay URL tu dong
Write-Host "[2/4] Dang khoi tao Cloudflare Tunnel ra Internet..." -ForegroundColor Yellow
$cloudflaredPath = "C:\Program Files (x86)\cloudflared\cloudflared.exe"
if (-not (Test-Path $cloudflaredPath)) {
    $cloudflaredCmd = Get-Command cloudflared -ErrorAction SilentlyContinue
    if ($cloudflaredCmd) { $cloudflaredPath = $cloudflaredCmd.Source }
}

$logFile = "$projectRoot\scratch\tunnel_live.log"
if (-not (Test-Path "$projectRoot\scratch")) { New-Item -ItemType Directory -Path "$projectRoot\scratch" -Force | Out-Null }
if (Test-Path $logFile) { Remove-Item $logFile -Force }

# Kiem tra xem co process cloudflared cu khong
$existingCloudflared = Get-Process -Name "cloudflared" -ErrorAction SilentlyContinue
if ($existingCloudflared) {
    Stop-Process -Name "cloudflared" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

# Khoi chay cloudflared ghi log ra file
$proc = Start-Process -FilePath $cloudflaredPath -ArgumentList "tunnel --url http://localhost:5000" -RedirectStandardError $logFile -WindowStyle Hidden -PassThru

$tunnelUrl = ""
$waitCount = 0
Write-Host "      -> Dang ket noi toi mang Cloudflare toan cau..." -NoNewline -ForegroundColor Gray
while ($waitCount -lt 15) {
    Start-Sleep -Seconds 1
    Write-Host "." -NoNewline -ForegroundColor Gray
    if (Test-Path $logFile) {
        $content = Get-Content $logFile -Raw -ErrorAction SilentlyContinue
        if ($content -match '(https:\/\/[a-zA-Z0-9-]+\.trycloudflare\.com)') {
            $tunnelUrl = $matches[1]
            break
        }
    }
    $waitCount++
}
Write-Host ""

if (-not $tunnelUrl) {
    Write-Host "      [CANH BAO] Khong the lay link tu dong sau 15 giay." -ForegroundColor Red
    Write-Host "      Vui long kiem tra file log: $logFile" -ForegroundColor Yellow
    exit 1
}

Write-Host "      [OK] Da tao thanh cong link Cloudflare HTTPS:" -ForegroundColor Green
Write-Host "      -> $tunnelUrl" -ForegroundColor Cyan -BackgroundColor Black

# 3. Tu dong cap nhat vao file .env cua Flutter
Write-Host "[3/4] Tu dong cap nhat link vao file fb_interface\.env..." -ForegroundColor Yellow
$envContent = @"
# API ket noi tu dong qua Cloudflare Tunnel (HTTPS an toan)
BASE_URL=$tunnelUrl/api/
# Du phong Local:
# BASE_URL=http://10.0.2.2:5000/api/
# BASE_URL=http://localhost:5000/api/
"@
$envPath = "$projectRoot\fb_interface\.env"
Set-Content -Path $envPath -Value $envContent -Encoding UTF8
Write-Host "      [OK] Da cap nhat BASE_URL=$tunnelUrl/api/ vao .env!" -ForegroundColor Green

# 4. Tu dong mo trinh duyet xem Swagger
Write-Host "[4/4] Tu dong mo Swagger API tren trinh duyet..." -ForegroundColor Yellow
Start-Process "$tunnelUrl/"

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "   DA HOAN TAT 100%! BAN KHONG CAN PHAI LAM BAT KY THAO TAC NAO!     " -ForegroundColor Yellow
Write-Host "======================================================================" -ForegroundColor Green
Write-Host " Link Swagger API : $tunnelUrl/" -ForegroundColor White
Write-Host " Link API Endpoint: $tunnelUrl/api/" -ForegroundColor White
Write-Host " App Flutter     : Da duoc cau hinh tu dong tro den link tren." -ForegroundColor White
Write-Host ""
Write-Host "Nhan Enter de giu man hinh hoac dong cua so nay (Tunnel van tiep tuc chay ngam)..."
Read-Host
