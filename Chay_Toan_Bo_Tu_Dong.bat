@echo off
chcp 65001 >nul
title [Bùi Nguyễn Công Nghiệp] Khởi Động Tự Động 100% Web API & Cloudflare Tunnel
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Chay_Toan_Bo_Tu_Dong.ps1"
