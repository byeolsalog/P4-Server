@echo off
setlocal

REM ---------------------
REM 서버 경로 지정
REM ---------------------
set ROOT_DIR=%~dp0
set GAME_DIR=%ROOT_DIR%GameServer
set WEB_DIR=%ROOT_DIR%WebServer

echo [START] WebServer 실행...
start "WebServer" cmd /k "cd /d %WEB_DIR% && dotnet run"

echo [START] GameServer 실행...
start "GameServer" cmd /k "cd /d %GAME_DIR% && dotnet run"

echo [DONE] 두 서버 실행 완료.
pause