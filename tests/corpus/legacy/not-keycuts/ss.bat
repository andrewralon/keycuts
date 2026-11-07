@ECHO OFF
SETLOCAL ENABLEDELAYEDEXPANSION
SET SSFOLDER="%SCREENSHOTS%"
PUSHD %SSFOLDER%

REM If no arg given, open base patch folder and close
REM Else confirm first argument is an integer
IF "%~1"=="" (
	GOTO :OpenFolder
) ELSE (
	SET "NTH="&FOR /F "delims=0123456789" %%I in ("%~1") DO SET "NTH=%%I"
	IF DEFINED NTH (
		ECHO "%1" is NOT a number.... Cannot continue.
		PAUSE
		GOTO :End
	) ELSE (
		REM ECHO "%1" is a number!
		GOTO :GetNthScreenshot
	)
)

:OpenFolder
"%SystemRoot%\explorer.exe" %SSFOLDER%
GOTO :End

:GetNthScreenshot
SET /A INDEX=1
REM ECHO INDEX = "!INDEX!"
FOR /F "delims=" %%I IN ('DIR . /B /O:-D') DO (
	IF !INDEX! EQU %1 (
		START "" /D %SSFOLDER% "%%I" & GOTO :End
	) 
	SET /A INDEX+=1
)
GOTO :End

:End
ENDLOCAL
POPD
EXIT
