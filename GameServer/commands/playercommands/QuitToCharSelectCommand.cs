
using System;
using DOL.GS;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [Cmd(
        "&quitchar",
        ePrivLevel.Player,
        "Instantly returns you to the character select screen.",
        "/quitchar")]
    public class QuitToCharSelectCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;

            if (player == null)
                return;

            client.Out.SendMessage("Returning you to the character selection screen...", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            client.Player.Quit(true); // Instantly go to char select
        }
    }
}
