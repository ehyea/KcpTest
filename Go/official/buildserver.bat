set GOPROXY=https://goproxy.cn
set GOBIN=%~dp0bin

go install ./src/server
pause