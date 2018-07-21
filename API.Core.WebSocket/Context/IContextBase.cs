﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace API.Core.WebSocket.Context
{
    public interface IBaseContext
    {
        Func<string, Task> Received { get; set; }
        Func<Task> Connected { get; set; }
        Func<Task> Reconnected { get; set; }
        Func<bool, Task> Disconnected { get; set; }
        string ConnectionID { get; set; }
    }
}
