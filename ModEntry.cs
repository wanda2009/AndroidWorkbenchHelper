using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;

namespace AndroidWorkbenchHelper
{
    // 1. KELAS KONFIGURASI MOD
    public class ModConfig
    {
        public int OutdoorRadius { get; set; } = 35;
        public bool ScanEntireRoom { get; set; } = true;
        public bool ConnectFarmBuildingsFromOutside { get; set; } = false;
        public bool FilterEmptyChests { get; set; } = true;
        public bool EnableQuickStackButton { get; set; } = true;
    }

    // 2. INTERFACE GENERIC MOD CONFIG MENU (GMCM)
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string> tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string> formatValue = null, string fieldId = null);
    }

    public class ModEntry : Mod
    {
        public ModConfig Config;
        private Rectangle quickStackBtnBox;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Display.MenuChanged += OnMenuChanged;
            helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        // INTEGRASI KE GENERIC MOD CONFIG MENU
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu != null)
            {
                configMenu.Register(
                    mod: this.ModManifest,
                    reset: () => this.Config = new ModConfig(),
                    save: () => this.Helper.WriteConfig(this.Config)
                );

                configMenu.AddSectionTitle(this.ModManifest, () => "Workbench Range Settings");

                configMenu.AddNumberOption(
                    mod: this.ModManifest,
                    getValue: () => this.Config.OutdoorRadius,
                    setValue: val => this.Config.OutdoorRadius = val,
                    name: () => "Outdoor Radius (Tiles)",
                    tooltip: () => "Radius jangkauan peti saat Workbench ditaruh di luar ladang (Default: 35).",
                    min: 5,
                    max: 100,
                    interval: 5
                );

                configMenu.AddBoolOption(
                    mod: this.ModManifest,
                    getValue: () => this.Config.ScanEntireRoom,
                    setValue: val => this.Config.ScanEntireRoom = val,
                    name: () => "Scan Entire Room (Indoors)",
                    tooltip: () => "Hubungkan semua peti di dalam ruangan yang sama (Rumah/Shed/Kandang)."
                );

                configMenu.AddBoolOption(
                    mod: this.ModManifest,
                    getValue: () => this.Config.ConnectFarmBuildingsFromOutside,
                    setValue: val => this.Config.ConnectFarmBuildingsFromOutside = val,
                    name: () => "Connect Sheds From Outside",
                    tooltip: () => "Sambungkan peti di dalam Shed saat kamu crafting di luar ladang."
                );

                configMenu.AddSectionTitle(this.ModManifest, () => "Performance & Features");

                configMenu.AddBoolOption(
                    mod: this.ModManifest,
                    getValue: () => this.Config.FilterEmptyChests,
                    setValue: val => this.Config.FilterEmptyChests = val,
                    name: () => "Filter Empty Chests (Anti-Lag)",
                    tooltip: () => "Lewati peti kosong agar prosesor HP tidak terbebani (Sangat disarankan ON)."
                );

                configMenu.AddBoolOption(
                    mod: this.ModManifest,
                    getValue: () => this.Config.EnableQuickStackButton,
                    setValue: val => this.Config.EnableQuickStackButton = val,
                    name: () => "Enable Quick-Stack Button",
                    tooltip: () => "Tampilkan tombol panah merah untuk auto-deposit barang ke peti."
                );
            }
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is CraftingPage craftingPage && !craftingPage.cooking)
            {
                var containerField = Helper.Reflection.GetField<List<IInventory>>(craftingPage, "_materialContainers");
                var existing = containerField.GetValue();

                if (existing != null)
                {
                    List<Chest> chests = GetChestsOptimized(Game1.currentLocation);
                    if (chests.Count > 0)
                    {
                        List<IInventory> containers = new List<IInventory>();
                        foreach (var c in chests)
                        {
                            if (c != null && c.Items != null)
                                containers.Add(c.Items);
                        }

                        containerField.SetValue(containers);
                        Monitor.Log($"Workbench tersambung ke {containers.Count} peti aktif!", LogLevel.Info);
                    }
                }
            }
        }

        private void OnRenderedActiveMenu(object sender, RenderedActiveMenuEventArgs e)
        {
            if (!Config.EnableQuickStackButton) return;

            if (Game1.activeClickableMenu is CraftingPage craftingPage && !craftingPage.cooking)
            {
                var containerField = Helper.Reflection.GetField<List<IInventory>>(craftingPage, "_materialContainers");
                if (containerField.GetValue() != null)
                {
                    int btnSize = 52;
                    // Posisi pas di samping kanan jendela menu crafting HP
                    int btnX = craftingPage.xPositionOnScreen + craftingPage.width - 64;
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
                        new Vector2(btnX + 6, btnY + 6),
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

            if (Config.EnableQuickStackButton && Game1.activeClickableMenu is CraftingPage craftingPage && !craftingPage.cooking)
            {
                Point mousePos = Game1.getMousePosition();
                Vector2 scaled = Utility.ModifyCoordinatesForUIScale(new Vector2(mousePos.X, mousePos.Y));
                Point uiPos = new Point((int)scaled.X, (int)scaled.Y);

                Rectangle touchArea = new Rectangle(quickStackBtnBox.X - 10, quickStackBtnBox.Y - 10, quickStackBtnBox.Width + 20, quickStackBtnBox.Height + 20);
                if (touchArea.Contains(mousePos) || touchArea.Contains(uiPos))
                {
                    Helper.Input.Suppress(e.Button);
                    QuickStackOptimized();
                }
            }
        }

        private void QuickStackOptimized()
        {
            List<Chest> chests = GetChestsOptimized(Game1.currentLocation);
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

        // PENGAMBILAN PETI CEPAT (ANTI-LAG 60 FPS)
        private List<Chest> GetChestsOptimized(GameLocation loc)
        {
            List<Chest> chests = new List<Chest>();
            if (loc == null) return chests;

            Vector2 playerPos = Game1.player.Tile;

            // 1. JIKA DI DALAM RUANGAN (Rumah, Shed, Kandang, Greenhouse)
            if (loc is FarmHouse || loc.Name == "Greenhouse" || loc.Name == "FarmCave" || loc.Name == "Cellar" || loc.Name.StartsWith("IslandFarmHouse") || IsBuildingInterior(loc))
            {
                if (Config.ScanEntireRoom)
                {
                    AddChestsFromLocation(loc, chests, null, -1);
                }
                else
                {
                    AddChestsFromLocation(loc, chests, playerPos, Config.OutdoorRadius);
                }
            }
            // 2. JIKA DI LUAR LADANG UTAMA
            else if (loc is Farm || loc.Name == "Farm")
            {
                AddChestsFromLocation(loc, chests, playerPos, Config.OutdoorRadius);

                if (Config.ConnectFarmBuildingsFromOutside)
                {
                    AddChestsFromLocation(Game1.getLocationFromName("FarmHouse"), chests, null, -1);
                    AddChestsFromLocation(Game1.getLocationFromName("Greenhouse"), chests, null, -1);

                    Farm farm = Game1.getFarm();
                    if (farm != null && farm.buildings != null)
                    {
                        foreach (var b in farm.buildings)
                        {
                            if (b.indoors.Value != null)
                                AddChestsFromLocation(b.indoors.Value, chests, null, -1);
                        }
                    }
                }
            }
            // 3. JIKA DI PULAU GINGER
            else if (loc.Name.StartsWith("IslandWest"))
            {
                AddChestsFromLocation(loc, chests, playerPos, Config.OutdoorRadius);
            }

            return chests;
        }

        private bool IsBuildingInterior(GameLocation loc)
        {
            Farm farm = Game1.getFarm();
            if (farm != null && farm.buildings != null)
            {
                foreach (var b in farm.buildings)
                {
                    if (b.indoors.Value != null && (b.indoors.Value == loc || b.indoors.Value.Name == loc.Name))
                        return true;
                }
            }
            return false;
        }

        private void AddChestsFromLocation(GameLocation location, List<Chest> list, Vector2? centerTile, int radius)
        {
            if (location == null || location.Objects == null) return;

            foreach (var kvp in location.Objects.Pairs)
            {
                if (kvp.Value is Chest chest && chest.Items != null)
                {
                    // Filter peti kosong untuk menghemat CPU HP (Anti-Lag)
                    if (Config.FilterEmptyChests && (chest.Items.Count == 0 || chest.isEmpty()))
                        continue;

                    if (centerTile.HasValue && radius > 0)
                    {
                        if (Vector2.Distance(centerTile.Value, kvp.Key) > radius)
                            continue;
                    }

                    list.Add(chest);
                }
            }
        }
    }
}
