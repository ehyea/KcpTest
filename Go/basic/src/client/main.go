package main

import kcp "github.com/xtaci/kcp-go"

func main() {
	kcpconn, err := kcp.DialWithOptions("localhost:10086", nil, 10, 3)
	if err!=nil {
		panic(err)
	}

	kcpconn.Write([]byte("hello kcp!"))
	select {}
}