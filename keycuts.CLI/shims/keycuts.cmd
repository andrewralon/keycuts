@REM keycuts on PATH: keycuts keeps this file in the keycuts folder, which is on PATH (#248).
@REM It runs keycuts.exe from the install folder the installer recorded. Changes to this file are replaced.
@SETLOCAL
@FOR /F "skip=2 tokens=2,*" %%A IN ('REG QUERY "HKCU\Software\TeamRalon\keycuts" /v InstallFolder 2^>NUL') DO @SET "KEYCUTS_DIR=%%B"
@IF NOT DEFINED KEYCUTS_DIR (ECHO keycuts isn't installed: install it again, or run keycuts.exe from its folder. 1>&2& EXIT /B 2)
@"%KEYCUTS_DIR%\keycuts.exe" %*
