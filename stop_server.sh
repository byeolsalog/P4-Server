#!/bin/bash
echo "[STOP] WebServer 중지"
pkill -f "dotnet run --project ~/P4-Server/WebServer"

echo "[STOP] GameServer 중지"
pkill -f "dotnet run --project ~/P4-Server/GameServer"

echo "[DONE] 두 서버가 중지되었습니다."