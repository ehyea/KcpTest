using System;

namespace Core.Socket
{
    public enum CloseResults
    {
        Pending,
        Closed,
        BeClosed
    }
    public enum OpenResults
    {
        Success = 0,
        Faild = -1
    }
    public enum SendResults
    {
        Success,
        Pending,
        Ignore,
        Faild
    }
    public enum SocketStatus
    {
        Initial = 1,
        Connecting,
        Establish,
        Closed
    }
}
