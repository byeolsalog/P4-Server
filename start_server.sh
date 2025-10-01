#!/bin/bash
set -e

echo "[START] WebServer 실행"
dotnet WebServer/WebServer.dll > webserver.log 2>&1 &
echo $! > webserver.pid

echo "[START] GameServer 실행"
dotnet GameServer/GameServer.dll > gameserver.log 2>&1 &
echo $! > gameserver.pid

echo "[DONE] 두 서버가 실행되었습니다."