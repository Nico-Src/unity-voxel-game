using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Toolbar : MonoBehaviour
{
    World World;
    public Player Player;

    // highlight and item references
    public RectTransform Highlight;
    public ItemSlot[] ItemSlots;

    int slotIndex = 0;

    private void Start()
    {
        World = GameObject.Find("World").GetComponent<World>();

        // initialize item slots
        foreach (ItemSlot slot in ItemSlots)
        {
            // set icon
            slot.icon.sprite = World.blockTypes[slot.ItemID].icon;
            slot.icon.enabled = true;
            slot.icon.gameObject.SetActive(true);
        }

        // set players selected index to the first slots item id
        Player.SelectedBlockIndex = ItemSlots[slotIndex].ItemID;
    }

    private void Update()
    {
        // get scroll value
        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");

        if(scroll != 0)
        {
            // upward
            if(scroll > 0)
            {
                slotIndex--;
            }
            else
            {
                slotIndex++;
            }

            if(slotIndex > ItemSlots.Length - 1)
            {
                slotIndex = 0;
            }

            if(slotIndex < 0)
            {
                slotIndex = ItemSlots.Length - 1;
            }

            // move highlight to selected slot
            Highlight.position = ItemSlots[slotIndex].icon.transform.position;
            Player.SelectedBlockIndex = ItemSlots[slotIndex].ItemID;
            // update block preview
            Player.ChangePlaceBlockTexture(World.blockTypes[ItemSlots[slotIndex].ItemID]);
        }
    }
}

[System.Serializable]
public class ItemSlot
{
    public byte ItemID;
    public Image icon;
}
