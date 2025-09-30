#!/bin/bash
echo "[START] WebServer 실행"
cd ~/P4-Server/WebServer
dotnet run &

echo "[START] GameServer 실행"
cd ~/P4-Server/GameServer
dotnet run &

echo "[DONE] 두 서버가 실행되었습니다."