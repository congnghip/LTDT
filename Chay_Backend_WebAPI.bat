@echo off
chcp 65001 >nul
title [Bùi Nguyễn Công Nghiệp] Máy Chủ Web API Chấm Công (Port 5000)
cd /d "%~dp0Sang6_WebAPI"
echo ==============================================================
echo   ĐANG KHỞI CHẠY WEB API CHẤM CÔNG THÔNG MINH (PORT 5000)...
echo   Hỗ trợ Face AI, GPS, Dynamic QR và Đo tiếng ồn môi trường
echo ==============================================================
dotnet run
pause
