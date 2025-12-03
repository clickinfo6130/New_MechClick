using System;
using System.Collections.Generic;

namespace Common.Protocol
{
    public abstract class IpcMessage
    {
        public string MessageType { get; set; }
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
    }

    public class ShowDialogRequest : IpcMessage
    {
        public ShowDialogRequest()
        {
            MessageType = "ShowDialog";
        }

        public string DialogType { get; set; }
        public Dictionary<string, object> Context { get; set; }
    }

    public class DialogResponse : IpcMessage
    {
        public DialogResponse()
        {
            MessageType = "DialogResponse";
        }

        public bool IsOk { get; set; }
        public Dictionary<string, object> SelectedData { get; set; }
    }
}