using System;
using System.IO;
using DOL.GS;
using DOL.Events;

namespace DOL.GS.Scripts
{
    public class ChatLogger
    {
        private static readonly string LogFilePath = Path.Combine("logs", "all_chat.log");

        [ScriptLoadedEvent]
        public static void OnScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            Directory.CreateDirectory("logs");

            GameEventMgr.AddHandler(GamePlayerEvent.Say, new DOLEventHandler(OnPlayerSay));
            GameEventMgr.AddHandler(GamePlayerEvent.WhisperReceive, new DOLEventHandler(OnWhisper));

            LogSystem("[ChatLogger] Loaded.");
        }

        [ScriptUnloadedEvent]
        public static void OnScriptUnloaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.RemoveHandler(GamePlayerEvent.Say, new DOLEventHandler(OnPlayerSay));
            GameEventMgr.RemoveHandler(GamePlayerEvent.WhisperReceive, new DOLEventHandler(OnWhisper));

            LogSystem("[ChatLogger] Unloaded.");
        }

        private static void LogSystem(string message)
        {
            string line = $"[System] {DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
            File.AppendAllText(LogFilePath, line + Environment.NewLine);
        }

        private static void LogChat(string type, string playerName, string message, string target = null)
        {
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string log = target == null
                ? $"[{time}] [{type}] {playerName}: {message}"
                : $"[{time}] [{type}] {playerName} -> {target}: {message}";

            File.AppendAllText(LogFilePath, log + Environment.NewLine);
        }

        private static void OnPlayerSay(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is GamePlayer player && args is SayEventArgs sayArgs)
                LogChat("Say", player.Name, sayArgs.Text);
        }

        private static void OnWhisper(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is GamePlayer player && args is WhisperReceiveEventArgs whisperArgs)
                LogChat("Tell", player.Name, whisperArgs.Text, whisperArgs.Source?.Name);
        }
    }
}
