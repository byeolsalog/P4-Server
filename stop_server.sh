#!/bin/bash
echo "[STOP] WebServer 중지"
pkill -f "dotnet run.*WebServer"

echo "[STOP] GameServer 중지"
pkill -f "dotnet run.*GameServer"

# 혹시 남은 dotnet 프로세스도 모두 정리
echo "[STOP] 잔여 dotnet 프로세스 확인 및 종료"
pkill -9 dotnet

echo "[DONE] 모든 서버 프로세스가 중지되었습니다."