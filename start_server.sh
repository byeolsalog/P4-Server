#!/bin/bash
set -e

echo "[START] WebServer 실행"
cd WebServer
dotnet bin/Debug/net8.0/WebServer.dll > ../webserver.log 2>&1 &
echo $! > ../webserver.pid
cd ..

echo "[START] GameServer 실행"
cd GameServer
dotnet bin/Debug/net8.0/GameServer.dll > ../gameserver.log 2>&1 &
echo $! > ../gameserver.pid
cd ..

echo "[DONE] 두 서버가 실행되었습니다."