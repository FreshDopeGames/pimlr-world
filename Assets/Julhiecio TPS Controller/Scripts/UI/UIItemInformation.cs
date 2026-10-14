using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using JUTPS.ItemSystem;
using JUTPS.WeaponSystem;

namespace JUTPS.InventorySystem.UI
{
    public class UIItemInformation : MonoBehaviour
    {
        private HoldableItem CurrentItem;
        private JUCharacterController Player;

        [Header("Essentials")]
        public Sprite EmptySprite;
        public Image Icon;
        public Text ItemName;
        public Text ItemQuantity;
        public GameObject BulletLabel;
        public Text BulletQuantity;
        public Image ItemHealth;
        void Start()
        {
            //Player = JUGameManager.InstancedPlayer;
        }

        // Update is called once per frame
        // PIMLR: every UI slot is optional. The gameplay HUD's "Weapons" panel has no Bullet Label any more
        // (the old ammo icon was deleted), and the first unassigned slot used to throw here every frame - which
        // stopped the ammo count below it from ever updating.
        void Update()
        {
            if (Player == null)
            {
                Player = JUGameManager.InstancedPlayer;
                return;
            }

            if (Player.Inventory == null) return;

            CurrentItem = Player.HoldableItemInUseRightHand;

            if (CurrentItem == null)
            {
                if (Icon) Icon.sprite = EmptySprite;
                if (BulletLabel) BulletLabel.SetActive(false);
                if (ItemName) ItemName.text = "Hand";
                if (ItemQuantity) ItemQuantity.text = "";
                if (ItemHealth) ItemHealth.fillAmount = 1;
            }
            else
            {
                if (CurrentItem is Weapon)
                {
                    if (Icon) Icon.sprite = CurrentItem.ItemIcon;
                    if (ItemName) ItemName.text = CurrentItem.ItemName;
                    if (ItemQuantity) ItemQuantity.text = CurrentItem.ItemQuantity + "/" + CurrentItem.MaxItemQuantity;

                    if (BulletLabel) BulletLabel.SetActive(true);
                    if (BulletQuantity) BulletQuantity.text = ((Weapon)CurrentItem).BulletsAmounts + "/" + ((Weapon)CurrentItem).TotalBullets;
                    if (ItemHealth) ItemHealth.fillAmount = (float)((Weapon)CurrentItem).BulletsAmounts / (float)((Weapon)CurrentItem).BulletsPerMagazine;
                    return;
                }

                if (CurrentItem is HoldableItem || CurrentItem is ThrowableItem)
                {
                    if (Icon) Icon.sprite = CurrentItem.ItemIcon;
                    if (ItemName) ItemName.text = CurrentItem.ItemName;
                    if (ItemQuantity) ItemQuantity.text = CurrentItem.ItemQuantity + "/" + CurrentItem.MaxItemQuantity;

                    if (BulletLabel) BulletLabel.SetActive(false);
                    if (ItemHealth) ItemHealth.fillAmount = (float)CurrentItem.ItemQuantity / (float)CurrentItem.MaxItemQuantity;
                }

                if (CurrentItem is MeleeWeapon)
                {
                    if (Icon) Icon.sprite = CurrentItem.ItemIcon;
                    if (ItemName) ItemName.text = CurrentItem.ItemName;
                    if (ItemQuantity) ItemQuantity.text = CurrentItem.ItemQuantity + "/" + CurrentItem.MaxItemQuantity;

                    if (BulletLabel) BulletLabel.SetActive(false);
                    if (ItemHealth) ItemHealth.fillAmount = (float)((MeleeWeapon)CurrentItem).MeleeWeaponHealth / 100;

                }
            }
        }
    }

}