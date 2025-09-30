@echo off
echo [STOP] 실행 중인 서버 종료...

REM WebServer 종료
taskkill /FI "WINDOWTITLE eq WebServer" /T /F

REM GameServer 종료
taskkill /FI "WINDOWTITLE eq GameServer" /T /F

echo [DONE] 서버 종료 완료.
pause