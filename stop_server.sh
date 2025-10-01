#!/bin/bash

echo "[STOP] WebServer 종료"
if [ -f webserver.pid ]; then
  kill -9 $(cat webserver.pid) 2>/dev/null || true
  rm -f webserver.pid
fi

echo "[STOP] GameServer 종료"
if [ -f gameserver.pid ]; then
  kill -9 $(cat gameserver.pid) 2>/dev/null || true
  rm -f gameserver.pid
fi

echo "[DONE] 모든 서버 프로세스가 중지되었습니다."