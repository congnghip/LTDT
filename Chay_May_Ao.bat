@echo off
chcp 65001 >nul
title [Bùi Nguyễn Công Nghiệp] Khởi Động Máy Ảo Android & App Chấm Công
set "ANDROID_HOME=C:\Users\buing.000\AppData\Local\Android\sdk"
set "ANDROID_SDK_ROOT=C:\Users\buing.000\AppData\Local\Android\sdk"
set "PATH=C:\Users\buing.000\AppData\Local\Android\sdk\emulator;C:\Users\buing.000\AppData\Local\Android\sdk\platform-tools;C:\Users\buing.000\develop\flutter\bin;%PATH%"

echo ==============================================================
echo   ĐANG KHỞI ĐỘNG MÁY ẢO ANDROID (Medium_Phone)...
echo ==============================================================
cd /d "C:\Users\buing.000\AppData\Local\Android\sdk\emulator"
start "" emulator.exe -avd Medium_Phone

echo Máy ảo đang được bật lên trên màn hình...
echo Đang chờ máy ảo khởi động (khoảng 15-20 giây)...
timeout /t 15 /nobreak >nul

echo.
echo ==============================================================
echo   ĐANG CÀI ĐẶT VÀ KHỞI CHẠY APP FLUTTER LÊN MÁY ẢO...
echo ==============================================================
cd /d "%~dp0fb_interface"
if not exist ".env" (
    if exist ".env.example" (
        copy ".env.example" ".env" >nul
        echo [INFO] Đã tự động tạo file .env từ .env.example
    )
)
flutter run
pause
