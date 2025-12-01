using System.Collections.Generic;

namespace PartManager.IPC
{
    /// <summary>
    /// IPC 통신용 메시지 클래스
    /// </summary>
    public class IPCMessage
    {
        public string Command { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public string MessageId { get; set; }
        public long Timestamp { get; set; }

        public IPCMessage()
        {
            Parameters = new Dictionary<string, object>();
            MessageId = System.Guid.NewGuid().ToString();
            Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// IPC 응답 메시지 클래스
    /// </summary>
    public class IPCResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public string MessageId { get; set; }

        public IPCResponse()
        {
            Data = new Dictionary<string, object>();
        }
    }
}
