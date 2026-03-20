@echo off
setlocal EnableExtensions

title DailyToDoList Publisher
for %%I in ("%~dp0..") do set "ROOT_DIR=%%~fI"
set "OUTPUT_DIR=%ROOT_DIR%\dist\win-x64"
set "LOG_PATH=%OUTPUT_DIR%\publish.log"

echo ======================================
echo   DailyToDoList Windows Publisher
echo ======================================
echo.
echo Root folder : %ROOT_DIR%
echo Output folder: %OUTPUT_DIR%
echo.

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] .NET 8 SDK is not installed or not on PATH.
  echo Install it from: https://dotnet.microsoft.com/download/dotnet/8.0
  echo.
  pause
  exit /b 1
)

pushd "%ROOT_DIR%"
if errorlevel 1 (
  echo [ERROR] Could not open the project folder.
  echo.
  pause
  exit /b 1
)

echo Publishing application...
echo A full log will be written to:
echo %LOG_PATH%
echo.

dotnet publish ".\DailyToDoList\DailyToDoList.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o "%OUTPUT_DIR%" > "%LOG_PATH%" 2>&1
set "PUBLISH_EXIT=%ERRORLEVEL%"
popd

if not "%PUBLISH_EXIT%"=="0" (
  echo [ERROR] Publish failed with exit code %PUBLISH_EXIT%.
  echo.
  echo Showing the publish log below:
  echo --------------------------------------
  type "%LOG_PATH%"
  echo --------------------------------------
  echo.
  pause
  exit /b %PUBLISH_EXIT%
)

echo [OK] Publish completed successfully.
echo.
echo Executable:
echo %OUTPUT_DIR%\DailyToDoList.exe
echo.
echo Build log:
echo %LOG_PATH%
echo.
pause
exit /b 0
