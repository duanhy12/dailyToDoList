@echo off
set ROOT_DIR=%~dp0..
set EXE_PATH=%ROOT_DIR%\dist\win-x64\DailyToDoList.exe

if not exist "%EXE_PATH%" (
  echo [ERROR] Published executable not found.
  echo Run scripts\publish-win-x64.bat first.
  exit /b 1
)

start "DailyToDoList" "%EXE_PATH%"
