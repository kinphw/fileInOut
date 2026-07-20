@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

rem ===== dpack build + zip packaging (double-click OK) =====
rem Version is read from the root VERSION file (single source).
rem Change VERSION and re-run to bump the exe/zip name automatically.

set "VER="
for /f "delims=" %%v in (VERSION) do if not defined VER set "VER=%%v"
if not defined VER (
  echo [ERROR] Cannot read root VERSION file.
  goto :fail
)

echo === Building dpack %VER% ===

rem 1) self-contained single exe (with single-file compression)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish --nologo
if errorlevel 1 goto :fail

rem 2) zip -> dist\dpack-<version>.zip  (exe + README)
if not exist dist mkdir dist
set "ZIP=dist\dpack-%VER%.zip"
if exist "%ZIP%" del /q "%ZIP%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path 'publish\dpack.exe','README.md' -DestinationPath '%ZIP%' -Force"
if errorlevel 1 goto :fail

echo.
echo === Done ===
echo   exe : publish\dpack.exe
echo   zip : %ZIP%
echo.
pause
endlocal
exit /b 0

:fail
echo.
echo *** BUILD FAILED ***
echo.
pause
endlocal
exit /b 1
