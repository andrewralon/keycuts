@ECHO OFF
SET SITE=https://example.com
IF EXIST "%USERPROFILE%\work.txt" SET SITE=https://work.example.com
START "" %SITE%
EXIT
