@ECHO OFF
REM keycuts 1.0.0.0
REM <shortcut>C:\Shortcuts\hosts.bat</shortcut>
REM <type>hostsfile</type>
REM <destination>C:\Windows\System32\drivers\etc\hosts</destination>
START "" /B "%windir%\system32\notepad.exe" "C:\Windows\System32\drivers\etc\hosts"
EXIT
