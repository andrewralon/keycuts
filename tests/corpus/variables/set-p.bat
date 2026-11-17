@ECHO OFF
SET /P TERM=Search for: 
SET "BASE=https://example.com/?q="
START "" %BASE%%TERM%
EXIT
