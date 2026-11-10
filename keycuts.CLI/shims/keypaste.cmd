@REM keypaste (#97): the old keypaste.exe is now "keycuts paste". This keeps the name working.
@REM It ships in the install folder's bin, the folder on PATH, so keycuts.exe is one folder up.
@"%~dp0..\keycuts.exe" paste %*
