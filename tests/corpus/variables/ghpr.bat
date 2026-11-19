@ECHO OFF
SET ORG=GolfNowEng
SET BASE=https://github.com/pulls?q=is%%3Aopen+org%%3A%ORG%
START "" %BASE%
EXIT
