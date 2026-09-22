@ECHO OFF
REM dequote.bat
REM References:
REM  * https://ss64.com/nt/syntax-dequote.html
FOR /f "delims=" %%A IN ('ECHO %%%1%%') DO SET %1=%%~A