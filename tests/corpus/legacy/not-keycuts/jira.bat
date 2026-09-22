@ECHO OFF
REM jira.bat
REM Purpose:  Opens any JIRA ticket
REM Usage:    jira ([ticket])
REM Examples: 
REM   jira
REM   jira CORE-123
REM   jira PROJECT-1337

REM SET JIRAURL=https://company.atlassian.net

IF '%1'=='' GOTO :HELP
GOTO :RUN

:HELP
ECHO jira [ticket]
ECHO Examples:
ECHO   jira
ECHO   jira KEY-1234
ECHO   jira CORE-1234
PAUSE
GOTO :EOF

:RUN
START %JIRAURL%/browse/%1
GOTO :EOF

:EOF
EXIT
