@echo off
chcp 65001 >nul
title [Bùi Nguyễn Công Nghiệp] Cloudflare Tunnel - Public API Chấm Công Ra Internet

echo ======================================================================
echo   CLOUDFLARE TUNNEL - TRIỂN KHAI PUBLIC ĐẦU API RA INTERNET (HTTPS)
echo   Dự án: Hệ Thống Chấm Công Thông Minh (Bùi Nguyễn Công Nghiệp)
echo ======================================================================
echo.

set "CLOUDFLARED_EXE=C:\Program Files (x86)\cloudflared\cloudflared.exe"

if not exist "%CLOUDFLARED_EXE%" (
    where cloudflared >nul 2>nul
    if %errorlevel% neq 0 (
        echo [LỖI] Không tìm thấy cloudflared.exe!
        echo Vui lòng cài đặt bằng lệnh: winget install --id Cloudflare.cloudflared
        pause
        exit /b 1
    ) else (
        set "CLOUDFLARED_EXE=cloudflared"
    )
)

echo [1] Đang kiểm tra Web API trên máy tính (port 5000)...
netstat -ano | findstr :5000 >nul 2>nul
if %errorlevel% neq 0 (
    echo [CẢNH BÁO] Chưa thấy Web API chạy trên Port 5000!
    echo Hãy đảm bảo đã chạy file: Chay_Backend_WebAPI.bat trước.
    echo.
) else (
    echo [OK] Đã phát hiện Web API đang chạy trên cổng 5000.
    echo.
)

echo [2] Đang khởi tạo Cloudflare Tunnel tới http://localhost:5000...
echo ----------------------------------------------------------------------
echo HƯỚNG DẪN SỬ DỤNG:
echo 1. Cloudflare sẽ tạo đường truyền và in ra URL dạng:
echo      https://xxxx-xxxx-xxxx.trycloudflare.com
echo 2. Copy URL đó và dán vào file fb_interface\.env:
echo      BASE_URL=https://xxxx-xxxx-xxxx.trycloudflare.com/api/
echo 3. Mở Swagger UI trên trình duyệt bất kỳ:
echo      https://xxxx-xxxx-xxxx.trycloudflare.com/
echo ----------------------------------------------------------------------
echo.

"%CLOUDFLARED_EXE%" tunnel --url http://localhost:5000
pause
