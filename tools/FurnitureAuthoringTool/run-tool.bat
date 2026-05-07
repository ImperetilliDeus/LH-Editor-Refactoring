@echo off
setlocal

set "ROOT=%~dp0"
set "EXE=%ROOT%artifacts\debug\FurnitureAuthoring.Tool.exe"

if not exist "%EXE%" (
    echo FurnitureAuthoring.Tool.exe was not found.
    echo Expected path: %EXE%
    exit /b 1
)

start "" "%EXE%"
