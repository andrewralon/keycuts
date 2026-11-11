@REM keycuts on PATH: the install folder's bin holds only these shims and is the folder on PATH, so the runtime
@REM DLLs beside keycuts.exe (one folder up) stay off it.
@"%~dp0..\keycuts.exe" %*
