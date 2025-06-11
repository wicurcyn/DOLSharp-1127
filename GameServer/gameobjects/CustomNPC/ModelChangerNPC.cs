using System;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Database;

namespace DOL.GS.Scripts
{
    public class ModelChangerNPC : GameNPC
    {
        public override bool AddToWorld()
        {
            Name = "Model Changer";
            Level = 50;
            Model = 544;
            return base.AddToWorld();
        }

        private InventoryItem _lastItemDropped;

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            GamePlayer player = source as GamePlayer;
            if (player == null || item == null)
                return false;

            _lastItemDropped = item;
            player.Out.SendMessage("Now whisper me the model number you want to apply to this item.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return true;
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            player.Out.SendMessage("To change the model of an item: Drop it on me, then whisper the new model number (e.g., 1234).", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            GamePlayer player = source as GamePlayer;
            if (player == null || _lastItemDropped == null)
                return false;

            if (!ushort.TryParse(text, out ushort newModel))
            {
                player.Out.SendMessage("Invalid model number. Please whisper a number like 1234.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // Clone the item
            InventoryItem clonedItem = GameServer.Database.FindObjectByKey<InventoryItem>(_lastItemDropped.ObjectId);
            if (clonedItem == null)
            {
                player.Out.SendMessage("Failed to find your item in the database.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            clonedItem = (InventoryItem)clonedItem.Clone();
            clonedItem.Model = newModel;
            clonedItem.OwnerID = player.InternalID;

            GameServer.Database.AddObject(clonedItem);
            player.Inventory.RemoveItem(_lastItemDropped);
            player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, clonedItem);

            player.Out.SendInventoryItemsUpdate(new InventoryItem[] { clonedItem });
            player.Out.SendMessage($"The model of your item has been changed to {newModel}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);

            _lastItemDropped = null;
            return true;
        }
    }
}