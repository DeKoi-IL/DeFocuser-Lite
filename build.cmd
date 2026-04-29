@echo off
rem build.cmd -- Forwards arguments to build.ps1 so the pipeline runs from
rem any cmd.exe prompt without manual PowerShell invocation.
rem
rem Usage:
rem    build.cmd 2.1.0
rem    build.cmd 2.1.0 -OutputDir D:\Releases
rem    build.cmd 2.1.0 -SkipBuild
rem    build.cmd 2.1.0 -Configuration Debug

setlocal

if "%~1"=="" (
    echo Usage: build.cmd ^<version^> [-OutputDir ^<dir^>] [-Configuration Debug^|Release] [-SkipBuild]
    echo Example: build.cmd 2.1.0
    exit /b 1
)

set "VERSION=%~1"
shift

set "FORWARD="
:collect
if "%~1"=="" goto run
set "FORWARD=%FORWARD% %1"
shift
goto collect

:run
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" -Version %VERSION%%FORWARD%
exit /b %ERRORLEVEL%
