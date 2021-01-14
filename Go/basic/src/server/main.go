package main

import (
	"fmt"
	kcp "github.com/xtaci/kcp-go"
	"io"
	"net"
)

func main() {
	fmt.Println("kcp listens on 10086")
	lis, err := kcp.ListenWithOptions(":10086", nil, 10, 3)
	if err!=nil {
		panic(err)
	}
	for {
		conn, e :=lis.AcceptKCP()
		if e!=nil {
			panic(e)
		}
		go func(conn net.Conn){
			var buffer = make([]byte,1024,1024)
			for {
				n,e :=conn.Read(buffer)
				if e!=nil {
					if e == io.EOF {
						break
					}
					fmt.Println(e.Error())
					break
				}

				fmt.Println("receive from client:", buffer[:n])
			}
		}(conn)
	}
}