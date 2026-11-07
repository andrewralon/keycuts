@ECHO OFF
REM newjira.bat
REM Purpose:  Opens a new OPS or CORE Jira ticket with common fields pre-populated
REM Requires: Change the JIRAUSER variable
REM Usage:    newjira ([KEY])
REM           newjira (OPS|CR|CORE|GN|G1|GC|GCD|QW)
REM Examples: newjira
REM           newjira GN
REM           newjira CORE
REM To use this script:
REM  1) Create C:\Shortcuts and add it to the system path
REM  2) Copy this script to C:\Shortcuts
REM  3) Change the JIRAUSER variable to your Jira username
REM  4) Type "Win+R" and "newjira <PROJECT_ABBREVIATION>"
REM    - Where <PROJECT_ABBREVIATION> is gn, g1, or gc

REM Change these:
REM SET JIRAUSER=firstname.lastname
SET JIRAUSER=%JIRAUSERNAME%

REM Do not change these:
SET ASSIGNEDDEVFIELD="^^^&customfield_11800"
SET ASSIGNEDDEVID=%JIRAUSER%
SET REQUESTORFIELD="^^^&customfield_19200"
SET REQUESTORID=15602
SET INFRASTRUCTUREFIELD="^^^&customfield_17600"
SET INFRASTRUCTUREID=
SET PRIORITYFIELD="^^^&customfield_14903"
SET PRIORITYID=12800
SET BUSINESSVALUEFIELD="^^^&customfield_14904"
SET BUSINESSVALUEID=12804
SET ASSIGNEDDEV="%ASSIGNEDDEVFIELD%=%JIRAUSER%"
SET REQUESTOR="%REQUESTORFIELD%=%REQUESTORID%"
SET PRIORITY="%PRIORITYFIELD%=%PRIORITYID%"
SET BUSINESSVALUE="%BUSINESSVALUEFIELD%=%BUSINESSVALUEID%"

REM Determine what commands to run
IF "%~1"=="" GOTO :OpsTicketStory
IF "%~1"=="ops" GOTO :OpsTicketStory
IF "%~1"=="OPS" GOTO :OpsTicketStory
IF "%~1"=="story" GOTO :OpsTicketStory
IF "%~1"=="STORY" GOTO :OpsTicketStory
IF "%~1"=="task" GOTO :OpsTicketTask
IF "%~1"=="TASK" GOTO :OpsTicketTask
IF "%~1"=="cr" GOTO :OpsChangeRequestTicket
IF "%~1"=="CR" GOTO :OpsChangeRequestTicket
IF "%~1"=="ec" GOTO :OpsEmergencyChangeTicket
IF "%~1"=="EC" GOTO :OpsEmergencyChangeTicket
IF "%~1"=="sr" GOTO :OpsSupportRequest
IF "%~1"=="SR" GOTO :OpsSupportRequest
IF "%~1"=="var" GOTO :OpsSupportRequestVariableTemplate
IF "%~1"=="VAR" GOTO :OpsSupportRequestVariableTemplate
IF "%~1"=="core" GOTO :CoreTicket
IF "%~1"=="CORE" GOTO :CoreTicket
IF "%~1"=="gn1" GOTO :G1Ticket
IF "%~1"=="GN1" GOTO :G1Ticket
IF "%~1"=="qw" GOTO :QWTicket
IF "%~1"=="QW" GOTO :QWTicket
IF "%~1"=="gn" SET INFRASTRUCTUREID=14900
IF "%~1"=="GN" SET INFRASTRUCTUREID=14900
IF "%~1"=="g1" SET INFRASTRUCTUREID=14901
IF "%~1"=="G1" SET INFRASTRUCTUREID=14901
IF "%~1"=="gc" SET INFRASTRUCTUREID=17136
IF "%~1"=="GC" SET INFRASTRUCTUREID=17136
IF "%~1"=="gcd" SET INFRASTRUCTUREID=17136
IF "%~1"=="GCD" SET INFRASTRUCTUREID=17136
REM GOTO :OpsTicketStory
GOTO :OpsChooseTicketType

:OpsChooseTicketType
SET PID=11900
IF NOT "%INFRASTRUCTUREID%"=="" (
  SET INFRASTRUCTURE="%INFRASTRUCTUREFIELD%=%INFRASTRUCTUREID%"
)
START %JIRAURL%/secure/CreateIssueDetails!init.jspa?pid=%PID%^&reporter=%JIRAUSER%^&assignee=%JIRAUSER%^&priority=6%ASSIGNEDDEV%%REQUESTOR%%INFRASTRUCTURE%
GOTO :EOF

:OpsTicketStory
SET PID=11900
REM issuetype=7 -> Story
IF NOT "%INFRASTRUCTUREID%"=="" (
  SET INFRASTRUCTURE="%INFRASTRUCTUREFIELD%=%INFRASTRUCTUREID%"
)
START %JIRAURL%/secure/CreateIssueDetails!init.jspa?pid=%PID%^&issuetype=7^&reporter=%JIRAUSER%^&assignee=%JIRAUSER%^&priority=6%ASSIGNEDDEV%%REQUESTOR%%INFRASTRUCTURE%
GOTO :EOF

:OpsTicketTask
SET PID=11900
REM issuetype=3 -> Task
IF NOT "%INFRASTRUCTUREID%"=="" (
  SET INFRASTRUCTURE="%INFRASTRUCTUREFIELD%=%INFRASTRUCTUREID%"
)
START %JIRAURL%/secure/CreateIssueDetails!init.jspa?pid=%PID%^&issuetype=3^&reporter=%JIRAUSER%^&assignee=%JIRAUSER%^&priority=6%ASSIGNEDDEV%%REQUESTOR%%INFRASTRUCTURE%
GOTO :EOF

:OpsChangeRequestTicket
SET PID=11900
SET ISSUETYPEID=12002
START %JIRAURL%/secure/CreateIssueDetails!init.jspa?pid=%PID%^&issuetype=%ISSUETYPEID%^&reporter=%JIRAUSER%^&assignee=%JIRAUSER%^&priority=6%ASSIGNEDDEV%%REQUESTOR%%INFRASTRUCTURE%
GOTO :EOF

:OpsEmergencyChangeTicket
SET PID=11900
SET ISSUETYPEID=12300
START %JIRAURL%/secure/CreateIssueDetails!init.jspa?pid=%PID%^&issuetype=%ISSUETYPEID%^&reporter=%JIRAUSER%^&assignee=%JIRAUSER%^&priority=6%ASSIGNEDDEV%%REQUESTOR%%INFRASTRUCTURE%
GOTO :EOF

:OpsSupportRequest
SET PID=11900
SET ISSUETYPEID=12801
START %JIRAURL%/secure/CreateIssueDetails!init.jspa?pid=%PID%^&issuetype=%ISSUETYPEID%^&reporter=%JIRAUSER%^&assignee=%JIRAUSER%^&priority=6%ASSIGNEDDEV%%REQUESTOR%%INFRASTRUCTURE%
REM https://example.atlassian.net/secure/CreateIssueDetails!init.jspa?pid=11900&issuetype=12801&reporter=user.name&assignee=user.name&priority=6&customfield_11800=user.name&customfield_19200=00000
GOTO :EOF

:OpsSupportRequestVariableTemplate
SET PID=11900
SET ISSUETYPEID=12801
SET TEMPLATE=18112
REM TEMPLATE=18812 -> CICD Variable
REM TEMPLATES INFO:
REM Specify a valid value for 'Template'. The allowed values are 18112[CI/CD Variable], 18077[DevOps Epic], 18081[DevOps Generic Story], 18082[DevOps Spike], 18083[DevOps Support], 18084[DevOps Task], 18110[DevOps CGA Remediation Story], 18076[Employee Offboarding], 18080[NSN - Decommission], -1
START %JIRAURL%/secure/CreateIssueDetails!init.jspa?pid=%PID%^&issuetype=%ISSUETYPEID%^&reporter=%JIRAUSER%^&assignee=%JIRAUSER%^&priority=6%ASSIGNEDDEV%%REQUESTOR%%INFRASTRUCTURE%^&customfield_21620=18112
GOTO :EOF

:CoreTicket
SET PID=10108
START %JIRAURL%/secure/CreateIssueDetails!init.jspa?pid=%PID%^&issuetype=3^&reporter=%JIRAUSER%^&assignee=%JIRAUSER%^&priority=6%ASSIGNEDDEV%%REQUESTOR%
GOTO :EOF

:G1Ticket
SET PID=16202
START %JIRAURL%/secure/CreateIssueDetails!init.jspa?pid=%PID%^&issuetype=3^&reporter=%JIRAUSER%^&assignee=%JIRAUSER%^&priority=6%REQUESTOR%
GOTO :EOF

:QWTicket
SET PID=18100
START %JIRAURL%/secure/CreateIssueDetails!init.jspa?pid=%PID%^&issuetype=3^&reporter=%JIRAUSER%^&assignee=%JIRAUSER%^&priority=6%REQUESTOR%%PRIORITY%%BUSINESSVALUE%
GOTO :EOF

:EOF
EXIT
