@echo off
set URL=https://www.google.com/
if not "%~1"=="" set "URL=https://www.google.com/search?q=%*"
start "" %URL%
