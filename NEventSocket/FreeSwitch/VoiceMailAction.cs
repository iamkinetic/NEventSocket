
namespace NEventSocket.FreeSwitch
{
    /// <summary>
    /// Based on actions found in:
    /// https://github.com/signalwire/freeswitch/blob/739e770c343db50a360f752a461da176c8a455ad/src/mod/applications/mod_voicemail/mod_voicemail.c
    /// </summary>
    public enum VoiceMailAction
    {
        MWIUpdate,
        FolderSummary,
        RemoveGreeting,
        ChangeGreeting,
        RecordGreeting,
        ChangePassword,
        RecordName,
        Authentication,
        LeaveMessage
    }
}
