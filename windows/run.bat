@echo off
REM Windows launcher. Usage: run.bat [start] [--en]
REM   run.bat          -> console + Spanish map at http://localhost:5000/
REM   run.bat D --en   -> console + English map at http://localhost:5000/en/
setlocal
cd /d "%~dp0..\DeliveryRoutes"
where dotnet >nul 2>nul || (echo .NET 8 SDK not found. Install it from https://dotnet.microsoft.com/download & exit /b 1)

set START=%1
if "%START%"=="" set START=D
set URLPATH=
if /I "%2"=="--en" set URLPATH=en/

start "" "http://localhost:5000/%URLPATH%"
dotnet run -- city.txt packages.txt %START% --web --no-open
endlocal
