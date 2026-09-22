@ECHO OFF
REM regeditopen.bat
REM Purpose:  Opens regedit.exe to a given location
REM Requires: N/A
REM Help:     regedit /?
REM Usage:    regeditopen ([path])
REM Examples: regeditopen
REM           regeditopen HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\System

IF '%1'=='' GOTO PROMPTPATH
GOTO GETPATH
GOTO RUN

:PROMPTPATH
SET /P REGPATH="Open regedit to path: "
:EOF

:GETPATH
SET REGPATH=%*
:EOF

:RUN
REG ADD HKCU\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit /v LastKey /t REG_SZ /d "%REGPATH%" /f
START regedit

:EOF