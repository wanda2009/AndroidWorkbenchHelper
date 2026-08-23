using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;

namespace AndroidWorkbenchHelper
{
    public class ModEntry : Mod
    {
        private Rectangle quickStackBtnBox;

        public override void Entry(IModHelper helper)
        {
            helper.Events.Display.MenuChanged += OnMenuChanged;
            helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is CraftingPage craftingPage && !craftingPage.cooking)
            {
                var containerField = Helper.Reflection.GetField<List<IInventory>>(craftingPage, "_materialContainers");
                var existing = containerField.GetValue();

                if (existing != null)
                {
                    List<Chest> chests = GetChestsForCurrentDomain(Game1.currentLocation);
                    if (chests.Count > 0)
                    {
                        List<IInventory> containers = new List<IInventory>();
                        foreach (var c in chests)
                        {
                            if (c != null && c.Items != null)
                                containers.Add(c.Items);
                        }

                        containerField.SetValue(containers);
                        Monitor.Log($"Workbench connected to {containers.Count} chests!", LogLevel.Info);
                    }
                }
            }
        }

        private void OnRenderedActiveMenu(object sender, RenderedActiveMenuEventArgs e)
        {
            if (Game1.activeClickableMenu is CraftingPage craftingPage && !craftingPage.cooking)
            {
                var containerField = Helper.Reflection.GetField<List<IInventory>>(craftingPage, "_materialContainers");
                if (containerField.GetValue() != null)
                {
                    int btnSize = 56;
                    int btnX = craftingPage.xPositionOnScreen + craftingPage.width + 10;
                    int btnY = craftingPage.yPositionOnScreen + 64;

                    quickStackBtnBox = new Rectangle(btnX, btnY, btnSize, btnSize);

                    IClickableMenu.drawTextureBox(
                        e.SpriteBatch,
                        Game1.menuTexture,
                        new Rectangle(0, 256, 60, 60),
                        btnX,
                        btnY,
                        btnSize,
                        btnSize,
                        Color.White,
                        1f,
                        false
                    );

                    e.SpriteBatch.Draw(
                        Game1.mouseCursors,
                        new Vector2(btnX + 8, btnY + 8),
                        new Rectangle(103, 469, 16, 16),
                        Color.White,
                        0f,
                        Vector2.Zero,
                        2.5f,
                        SpriteEffects.None,
                        0.9f
                    );
                }
            }
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || e.Button != SButton.MouseLeft) return;

            if (Game1.activeClickableMenu is CraftingPage craftingPage && !craftingPage.cooking)
            {
                Point mousePos = Game1.getMousePosition();
                Vector2 scaled = Utility.ModifyCoordinatesForUIScale(new Vector2(mousePos.X, mousePos.Y));
                Point uiPos = new Point((int)scaled.X, (int)scaled.Y);

                Rectangle touchArea = new Rectangle(quickStackBtnBox.X - 10, quickStackBtnBox.Y - 10, quickStackBtnBox.Width + 20, quickStackBtnBox.Height + 20);
                if (touchArea.Contains(mousePos) || touchArea.Contains(uiPos))
                {
                    Helper.Input.Suppress(e.Button);
                    QuickStackToDomainChests();
                }
            }
        }

        private void QuickStackToDomainChests()
        {
            List<Chest> chests = GetChestsForCurrentDomain(Game1.currentLocation);
            if (chests.Count == 0) return;

            int movedCount = 0;

            for (int i = 0; i < Game1.player.Items.Count; i++)
            {
                Item playerItem = Game1.player.Items[i];
                if (playerItem == null) continue;

                if (playerItem is Tool || playerItem is MeleeWeapon || playerItem is Slingshot || playerItem is FishingRod)
                    continue;

                foreach (var chest in chests)
                {
                    if (chest == null || chest.Items == null) continue;

                    for (int c = 0; c < chest.Items.Count; c++)
                    {
                        Item chestItem = chest.Items[c];
                        if (chestItem != null && chestItem.canStackWith(playerItem))
                        {
                            int remaining = chestItem.addToStack(playerItem);
                            if (remaining <= 0)
                            {
                                Game1.player.Items[i] = null;
                                movedCount++;
                                break;
                            }
                            else
                            {
                                playerItem.Stack = remaining;
                            }
                        }
                    }

                    if (Game1.player.Items[i] == null)
                        break;
                }
            }

            if (movedCount > 0)
            {
                Game1.playSound("Ship");
                Game1.showGlobalMessage("Deposited items to matching chests!");
            }
            else
            {
                Game1.playSound("cancel");
                Game1.showGlobalMessage("No matching items to deposit.");
            }
        }

        private List<Chest> GetChestsForCurrentDomain(GameLocation loc)
        {
            List<Chest> chests = new List<Chest>();
            if (loc == null) return chests;

            if (IsMainFarmDomain(loc))
            {
                AddChestsFromLocation(Game1.getFarm(), chests);
                AddChestsFromLocation(Game1.getLocationFromName("FarmHouse"), chests);
                AddChestsFromLocation(Game1.getLocationFromName("Greenhouse"), chests);
                AddChestsFromLocation(Game1.getLocationFromName("FarmCave"), chests);
                AddChestsFromLocation(Game1.getLocationFromName("Cellar"), chests);

                if (Game1.getFarm()?.buildings != null)
                {
                    foreach (var b in Game1.getFarm().buildings)
                    {
                        if (b.indoors.Value != null)
                            AddChestsFromLocation(b.indoors.Value, chests);
                    }
                }
            }
            else if (IsGingerIslandDomain(loc))
            {
                AddChestsFromLocation(Game1.getLocationFromName("IslandWest"), chests);
                AddChestsFromLocation(Game1.getLocationFromName("IslandFarmHouse"), chests);
            }

            return chests;
        }

        private bool IsMainFarmDomain(GameLocation loc)
        {
            if (loc is Farm || loc is FarmHouse) return true;
            string name = loc.Name ?? "";
            if (name == "Farm" || name == "FarmHouse" || name == "Greenhouse" || name == "FarmCave" || name == "Cellar") return true;

            if (Game1.getFarm()?.buildings != null)
            {
                foreach (var b in Game1.getFarm().buildings)
                {
                    if (b.indoors.Value != null && (b.indoors.Value == loc || b.indoors.Value.Name == name))
                        return true;
                }
            }
            return false;
        }

        private bool IsGingerIslandDomain(GameLocation loc)
        {
            string name = loc.Name ?? "";
            return name.StartsWith("IslandWest") || name.StartsWith("IslandFarmHouse");
        }

        private void AddChestsFromLocation(GameLocation location, List<Chest> list)
        {
            if (location == null || location.Objects == null) return;
            foreach (var obj in location.Objects.Values)
            {
                if (obj is Chest chest && chest.Items != null)
                    list.Add(chest);
            }
        }
    }
}
