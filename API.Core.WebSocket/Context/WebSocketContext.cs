﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using API.Core.WebSocket.InternalStructure;

namespace API.Core.WebSocket.Context
{
    public abstract class WebSocketContext : IContextBase
    {
        private readonly TimeSpan _closeTimeout = TimeSpan.FromMilliseconds(250);
        private System.Net.WebSockets.WebSocket _webSocket;
        private CancellationToken _disconnectToken;
        public virtual void OnOpen() { }
        public virtual void OnMessage(string message) { throw new NotImplementedException(); }
        public virtual void OnMessage(byte[] message) { throw new NotImplementedException(); }
        public virtual Task OnSend(Message message) { throw new NotImplementedException(); }
        public virtual void OnError() { }
        public virtual void OnClosed() { }

        public Func<string, Task> Received { get; set; }
        public Func<Task> Connected { get; set; }
        public Func<Task> Reconnected { get; set; }
        public string ConnectionID { get; set; }
        public Func<bool, Task> Disconnected { get; set; }

        public IContextConnection Connection { get; private set; }

        public Task ProcessReqeust(System.Net.WebSockets.WebSocket webSocket, IContextConnection connection)
        {
            Connection = connection;
            _webSocket = webSocket;
            _disconnectToken = new CancellationToken();
            OnOpen();
            Connection.Send(OnSend, this);
            return ReceiveAsync(_webSocket, _disconnectToken);
        }
        private async Task ReceiveAsync(System.Net.WebSockets.WebSocket webSocket, CancellationToken disconnectToken)
        {
            bool closedReceived = false;
            var buffer = new byte[4096];

            try
            {
                while (!disconnectToken.IsCancellationRequested && !closedReceived)
                {
                    var arraySegment = new ArraySegment<byte>(buffer);
                    var result = await webSocket.ReceiveAsync(arraySegment, disconnectToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        closedReceived = true;
                        await Task.WhenAny(CloseAsync(), Task.Delay(_closeTimeout));
                    }
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        OnMessage(Encoding.UTF8.GetString(arraySegment.Array.Take(result.Count).ToArray()));
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        OnMessage(arraySegment.Array.Take(result.Count).ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Error Receiving : " + ex.Message);
            }
            try
            {
                await CloseAsync();
            }
            finally
            {
                OnClosed();
            }

        }
        private async Task SendAsync(ArraySegment<byte> message, WebSocketMessageType messageType, bool endOfMessage = true)
        {
            if (_webSocket.State != WebSocketState.Open)
                return;

            try
            {
                await _webSocket.SendAsync(message, messageType, endOfMessage, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error Sending : " + ex.Message);
            }
        }
        public virtual Task CloseAsync()
        {
            if (_webSocket.State == WebSocketState.Closed ||
                _webSocket.State == WebSocketState.Aborted ||
                _webSocket.State == WebSocketState.CloseSent)
            {
                return Task.CompletedTask;
            }

            try
            {
                _disconnectToken.ThrowIfCancellationRequested();
                return _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error Closing : " + ex.Message);
            }
            return Task.CompletedTask;
        }

        public Task Send(Message message)
        {
            message.ConnectionID = ConnectionID;
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(message)));
            return _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
