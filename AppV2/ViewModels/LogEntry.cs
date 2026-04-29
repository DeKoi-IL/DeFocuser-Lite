using System;

namespace ASCOM.DeKoi.DeFocuserApp.ViewModels
{
    public enum LogKind { Info, Send, Recv, Ok, Err, Warn }

    public class LogEntry
    {
        public string Timestamp { get; }
        public LogKind Kind { get; }
        public string KindLabel => Kind.ToString().ToUpperInvariant();
        public string Message { get; }

        public LogEntry(LogKind kind, string message)
        {
            Kind = kind;
            Message = message ?? string.Empty;
            Timestamp = DateTime.Now.ToString("HH:mm:ss");
        }
    }
}
