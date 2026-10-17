@echo off
setlocal enabledelayedexpansion

echo === RideReady local run ===

REM --- 1. Stop any existing stack from a previous run ---
echo Stopping any existing containers...
docker compose -f docker-compose.yml -f docker-compose.local.yml down >nul 2>&1

REM --- 2. Make sure Docker Desktop is running ---
docker info >nul 2>&1
if errorlevel 1 (
    echo Docker daemon not running - starting Docker Desktop...
    if exist "C:\Program Files\Docker\Docker\Docker Desktop.exe" (
        start "" "C:\Program Files\Docker\Docker\Docker Desktop.exe"
    ) else (
        echo Could not find Docker Desktop.exe - please start Docker manually and re-run this script.
        exit /b 1
    )

    echo Waiting for Docker to be ready...
    set /a dockerwait=0
    :waitdocker
    timeout /t 3 >nul
    docker info >nul 2>&1
    if not errorlevel 1 goto dockerready
    set /a dockerwait+=1
    if !dockerwait! GEQ 40 (
        echo Docker did not become ready in time ^(2 minutes^). Aborting.
        exit /b 1
    )
    goto waitdocker
    :dockerready
    echo Docker is ready.
)

REM --- 3. Local-only .env for docker-compose (gitignored, dev credentials only) ---
if not exist .env (
    echo Creating local .env for docker-compose...
    (
        echo IMAGE_NAME=rideready
        echo IMAGE_TAG=local
        echo DB_USER=rideuser
        echo DB_PASSWORD=devpassword123
    ) > .env
)

REM --- 4. Build the app image locally (context = App/, per the Task 10 Dockerfile fix) ---
echo Building app image...
docker build -t rideready:local -f App\Dockerfile App
if errorlevel 1 (
    echo Docker build failed.
    exit /b 1
)

REM --- 5. Start app + postgres ---
echo Starting containers...
docker compose -f docker-compose.yml -f docker-compose.local.yml up -d
if errorlevel 1 (
    echo docker compose up failed.
    exit /b 1
)

REM --- 6. Wait for the app to report healthy ---
echo Waiting for app to become healthy...
set /a healthwait=0
:waithealth
curl -sf http://localhost:5000/health >nul 2>&1
if not errorlevel 1 goto healthy
set /a healthwait+=1
if !healthwait! GEQ 30 (
    echo App did not become healthy in time. Recent logs:
    docker compose -f docker-compose.yml -f docker-compose.local.yml logs --tail=50 app
    exit /b 1
)
timeout /t 2 >nul
goto waithealth

:healthy
echo App is healthy.

REM --- 7. Let other devices on the same Wi-Fi reach it (local testing only) ---
set "LANIP="
for /f "tokens=2 delims=:" %%a in ('ipconfig ^| findstr /C:"IPv4"') do (
    for /f "tokens=* delims= " %%b in ("%%a") do (
        if not defined LANIP echo %%b| findstr /B "192.168. 10." >nul && set "LANIP=%%b"
    )
)

netsh advfirewall firewall show rule name="RideReady dev 5000" >nul 2>&1
if errorlevel 1 (
    netsh advfirewall firewall add rule name="RideReady dev 5000" dir=in action=allow protocol=TCP localport=5000 profile=any remoteip=localsubnet >nul 2>&1
    if errorlevel 1 (
        echo.
        echo NOTE: Windows Firewall is blocking other devices. Run this ONCE in an Administrator Command Prompt:
        echo   netsh advfirewall firewall add rule name="RideReady dev 5000" dir=in action=allow protocol=TCP localport=5000 profile=any remoteip=localsubnet
    ) else (
        echo Firewall rule added for TCP 5000 for devices on your local network.
    )
)

REM --- 8. Open it ---
start "" http://localhost:5000

echo.
echo Done. RideReady is running at http://localhost:5000
echo   Admin login:  http://localhost:5000/AdminAuth/Login   (admin / ChangeMe123^^!)
echo   Driver login: http://localhost:5000/DriverAuth/Login
if defined LANIP (
    echo.
    echo   On your iPad or phone ^(same Wi-Fi^):  http://!LANIP!:5000
    echo   Note: plain HTTP, so the driver "Share my location" button is blocked on phones.
) else (
    echo.
    echo   Could not find this PC's Wi-Fi address. Run ipconfig and use http://^<IPv4 address^>:5000
)
echo   Local testing only - WhatsApp is switched off in this copy.
echo   Stop with:    docker compose -f docker-compose.yml -f docker-compose.local.yml down

endlocal
