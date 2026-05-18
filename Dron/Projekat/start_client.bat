@echo off
echo ========================================
echo    DRONE TELEMETRY CLIENT STARTER
echo ========================================
echo.
echo IMPORTANT: Make sure the server is running first!
echo You can start it by running: start_server.bat
echo.
echo Starting Drone Telemetry Client...
echo Client will connect to: net.tcp://localhost:4100/Drone
echo.

cd /d "%~dp0Client\bin\Debug"
if not exist Client.exe (
    echo ERROR: Client.exe not found!
    echo Please build the solution first using:
    echo   dotnet build Dron.sln
    echo.
    pause
    exit /b 1
)

echo ========================================
echo.
Client.exe
echo.
echo ========================================
echo Client finished. Press any key to exit...
pause >nul
