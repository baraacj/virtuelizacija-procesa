@echo off
echo ========================================
echo    DRONE TELEMETRY SERVER STARTER
echo ========================================
echo.
echo Starting Drone Telemetry Server...
echo Server will listen on: net.tcp://localhost:4100/Drone
echo.

cd /d "%~dp0Server\bin\Debug"
if not exist Server.exe (
    echo ERROR: Server.exe not found!
    echo Please build the solution first using:
    echo   dotnet build Dron.sln
    echo.
    pause
    exit /b 1
)

echo Press Ctrl+C to stop the server
echo ========================================
echo.
Server.exe
