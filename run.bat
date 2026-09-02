@echo off
setlocal enabledelayedexpansion

echo === RideReady local run ===

REM --- 1. Stop any existing stack from a previous run ---
echo Stopping any existing containers...
docker compose down >nul 2>&1

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

REM --- 4. Build the app image locally (context = RideBooking/, per the Task 10 Dockerfile fix) ---
echo Building app image...
docker build -t rideready:local -f RideBooking\Dockerfile RideBooking
if errorlevel 1 (
    echo Docker build failed.
    exit /b 1
)

REM --- 5. Start app + postgres ---
echo Starting containers...
docker compose up -d
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
    docker compose logs --tail=50 app
    exit /b 1
)
timeout /t 2 >nul
goto waithealth

:healthy
echo App is healthy.

REM --- 7. Open it ---
start "" http://localhost:5000

echo Done. RideReady is running at http://localhost:5000
echo   Admin login:  http://localhost:5000/AdminAuth/Login
echo   Driver login: http://localhost:5000/DriverAuth/Login
echo   Stop with:    docker compose down

endlocal
