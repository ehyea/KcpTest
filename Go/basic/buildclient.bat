set GOPROXY=https://goproxy.io
set GOBIN=%~dp0bin

go install ./src/client
pause