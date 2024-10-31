using System;
using NEventSocket.Util;

namespace NEventSocket.FreeSwitch
{
    public class VoiceMailEvent : EventMessage
    {
        protected internal VoiceMailEvent(EventMessage other) : base(other)
        {
            if (other.EventName != EventName.Custom && other.Headers[HeaderNames.EventSubclass] != CustomEvents.VoiceMail.Maintenance)
            {
                throw new InvalidOperationException(
                    "Expected event of type Custom with SubClass vm::maintainance, got {0} instead".Fmt(other.EventName));
            }
        }

        public VoiceMailAction Action
        {
            get
            {
                if (Headers.TryGetValue(HeaderNames.VoiceMail.Action, out string headerValue))
                {
                    if (Enum.TryParse(headerValue.Replace("-", string.Empty).Replace("_", string.Empty), true, out VoiceMailAction action))
                    {
                        return action;
                    }
                    throw new NotSupportedException("Unable to parse VoiceMail Action '{0}'.".Fmt(headerValue));
                }

                throw new InvalidOperationException("Event did not contain an Action Header");
            }
        }

        public string User
        {
            get
            {
                return Headers[HeaderNames.VoiceMail.User];
            }
        }

        public string Domain
        {
            get
            {
                return Headers[HeaderNames.VoiceMail.Domain];
            }
        }

        public string TotalNew
        {
            get
            {
                return Headers[HeaderNames.VoiceMail.TotalNew];
            }
        }

        public string TotalSaved
        {
            get
            {
                return Headers[HeaderNames.VoiceMail.TotalSaved];
            }
        }

        public string TotalSavedUrgent
        {
            get
            {
                return Headers[HeaderNames.VoiceMail.TotalSavedUrgent];
            }
        }

        public string TotalNewUrgent
        {
            get
            {
                return Headers[HeaderNames.VoiceMail.TotalNewUrgent];
            }
        }

        public string CallerIdName
        {
            get
            {
                return Headers[HeaderNames.VoiceMail.CallerIdName];
            }
        }

        public string CallerIdNumber
        {
            get
            {
                return Headers[HeaderNames.VoiceMail.CallerIdNumber];
            }
        }

        public string Folder
        {
            get
            {
                return Headers[HeaderNames.VoiceMail.Folder];
            }
        }

        public string FilePath
        {
            get
            {
                return Headers[HeaderNames.VoiceMail.FilePath];
            }
        }

        public string Flags
        {
            get
            {
                return Headers[HeaderNames.VoiceMail.Flags];
            }
        }

        public string MessageLength
        {
            get
            {
                return Headers[HeaderNames.VoiceMail.MessageLength];
            }
        }

        public string Uuid
        {
            get
            {
                return Headers[HeaderNames.VoiceMail.Uuid];
            }
        }

        public string TimeStamp
        {
            get
            {
                return Headers[HeaderNames.VoiceMail.Timestamp];
            }
        }
            
        public override string ToString()
        {
            return $"VoiceMailEvent: {Action} {User} {Domain} {TotalNew} {TotalSaved} {TotalSavedUrgent} {TotalNewUrgent} {CallerIdName} {CallerIdNumber} {Folder} {FilePath} {Flags} {MessageLength} {Uuid} {TimeStamp}";
        }
    }
}