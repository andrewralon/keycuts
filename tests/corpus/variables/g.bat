SET ORG=GolfNowEng
SET BASE=https://github.com/%ORG%
SET QUERY=

IF NOT "%~1"=="" (
	SET BASE=https://github.com/search?q=org%%3A%ORG%
	SET "QUERY=+%*^&type=repositories"
)
START "" %BASE%%QUERY%
