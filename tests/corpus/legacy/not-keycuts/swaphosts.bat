@ECHO OFF

SET FILENAME=%~n0

REM Run as user
REM START powershell.exe -Command "& '%FILENAME%.ps1' %*"

REM Run as admin
START nircmdc.exe elevate powershell.exe -Command "& '%FILENAME%.ps1' %* -fromCMD"

REM PAUSE
EXIT
