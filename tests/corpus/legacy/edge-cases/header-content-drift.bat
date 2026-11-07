@ECHO OFF
REM keycuts 1.0.0.0
REM <shortcut>C:\Shortcuts\drift.bat</shortcut>
REM <type>url</type>
REM <destination>https://example.com</destination>
START "" /D "" "chrome" "? %*"
EXIT
