@echo off
setlocal

set ROOT_DIR=%~dp0..
set OUTPUT_DIR=%ROOT_DIR%\dist\win-x64

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] .NET 8 SDK is not installed or not on PATH.
  echo Install it from: https://dotnet.microsoft.com/download/dotnet/8.0
  exit /b 1
)

pushd "%ROOT_DIR%"
dotnet publish .\DailyToDoList\DailyToDoList.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o "%OUTPUT_DIR%"
set PUBLISH_EXIT=%ERRORLEVEL%
popd

if not "%PUBLISH_EXIT%"=="0" (
  echo [ERROR] Publish failed.
  exit /b %PUBLISH_EXIT%
)

echo.
echo [OK] Published executable:
echo %OUTPUT_DIR%\DailyToDoList.exe
exit /b 0
