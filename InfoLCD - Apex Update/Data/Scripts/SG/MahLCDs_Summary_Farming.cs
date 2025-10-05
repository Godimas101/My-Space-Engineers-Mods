using MahrianeIndustries.LCDInfo;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems.TextSurfaceScripts;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Utils;
using VRageMath;

namespace MahrianeIndustries.LCDInfo
{
    [MyTextSurfaceScript("LCDInfoScreenFarmingSummary", "$IOS LCD - Farming")]
    public class LCDFarmingSummaryInfo : MyTextSurfaceScriptBase
    {
        public static string CONFIG_SECTION_ID = "SettingsFarmingSummary";

        MyIni config = new MyIni();
        SurfaceDrawer.SurfaceData surfaceData;
        IMyTextSurface mySurface;
        IMyTerminalBlock myTerminalBlock;

        // Configurable toggles
        bool showFarmPlots = true;
        bool showAlgaeFarms = true;
        bool showIrrigationBlocks = true;

        // Counters
        int totalFarmPlots = 0;
        int algaeFarmCount = 0;
        int irrigationBlockCount = 0;

        // Search and exclusion
        string searchId = "*";
        List<string> excludeIds = new List<string>();
        bool compactMode = false;
        Sandbox.ModAPI.Ingame.MyShipMass gridMass;

        public LCDFarmingSummaryInfo(IMyTextSurface surface, IMyCubeBlock block, Vector2 size) : base(surface, block, size)
        {
            mySurface = surface;
            myTerminalBlock = block as IMyTerminalBlock;
        }

        public override ScriptUpdate NeedsUpdate => ScriptUpdate.Update100;

        void TryCreateSurfaceData()
        {
            if (surfaceData != null) return;

            compactMode = mySurface.SurfaceSize.X / mySurface.SurfaceSize.Y > 4f;
            float textSize = compactMode ? 0.6f : 0.4f;

            surfaceData = new SurfaceDrawer.SurfaceData
            {
                surface = mySurface,
                textSize = textSize,
                titleOffset = 240,
                ratioOffset = 82,
                viewPortOffsetX = compactMode ? 10 : 20,
                viewPortOffsetY = compactMode ? 5 : 10,
                newLine = new Vector2(0, 30 * textSize),
                showHeader = true,
                showSummary = true,
                showMissing = false,
                showRatio = false,
                showBars = false,
                showSubgrids = true,
                showDocked = false,
                useColors = true
            };
        }

        public override void Run()
        {
            if (myTerminalBlock.CustomData.Length <= 0 || !myTerminalBlock.CustomData.Contains(CONFIG_SECTION_ID))
                CreateConfig();

            LoadConfig();
            UpdateBlocks();
            Draw();
        }

        void CreateConfig()
        {
            TryCreateSurfaceData();
            config.Clear();
            config.AddSection(CONFIG_SECTION_ID);
            config.Set(CONFIG_SECTION_ID, "SearchId", "*");
            config.Set(CONFIG_SECTION_ID, "ExcludeIds", "");
            config.Set(CONFIG_SECTION_ID, "ShowFarmPlots", showFarmPlots.ToString());
            config.Set(CONFIG_SECTION_ID, "ShowAlgaeFarms", showAlgaeFarms.ToString());
            config.Set(CONFIG_SECTION_ID, "ShowIrrigationBlocks", showIrrigationBlocks.ToString());
            config.Set(CONFIG_SECTION_ID, "ShowHeader", surfaceData.showHeader.ToString());
            config.Set(CONFIG_SECTION_ID, "ShowSummary", surfaceData.showSummary.ToString());
            config.Set(CONFIG_SECTION_ID, "TextSize", surfaceData.textSize.ToString());
            config.Set(CONFIG_SECTION_ID, "ViewPortOffsetX", surfaceData.viewPortOffsetX.ToString());
            config.Set(CONFIG_SECTION_ID, "ViewPortOffsetY", surfaceData.viewPortOffsetY.ToString());
            config.Set(CONFIG_SECTION_ID, "TitleFieldWidth", surfaceData.titleOffset.ToString());
            config.Set(CONFIG_SECTION_ID, "RatioFieldWidth", surfaceData.ratioOffset.ToString());
            config.Set(CONFIG_SECTION_ID, "UseColors", surfaceData.useColors.ToString());

            config.Invalidate();
            myTerminalBlock.CustomData += "\n" + config.ToString() + "\n";
        }

        void LoadConfig()
        {
            try
            {
                MyIniParseResult result;
                TryCreateSurfaceData();
                if (config.TryParse(myTerminalBlock.CustomData, CONFIG_SECTION_ID, out result))
                {
                    if (config.ContainsKey(CONFIG_SECTION_ID, "ShowHeader")) surfaceData.showHeader = config.Get(CONFIG_SECTION_ID, "ShowHeader").ToBoolean();
                    if (config.ContainsKey(CONFIG_SECTION_ID, "ShowSummary")) surfaceData.showSummary = config.Get(CONFIG_SECTION_ID, "ShowSummary").ToBoolean();
                    if (config.ContainsKey(CONFIG_SECTION_ID, "TextSize")) surfaceData.textSize = config.Get(CONFIG_SECTION_ID, "TextSize").ToSingle(0.4f);
                    if (config.ContainsKey(CONFIG_SECTION_ID, "TitleFieldWidth")) surfaceData.titleOffset = config.Get(CONFIG_SECTION_ID, "TitleFieldWidth").ToInt32();
                    if (config.ContainsKey(CONFIG_SECTION_ID, "RatioFieldWidth")) surfaceData.ratioOffset = config.Get(CONFIG_SECTION_ID, "RatioFieldWidth").ToInt32();
                    if (config.ContainsKey(CONFIG_SECTION_ID, "ViewPortOffsetX")) surfaceData.viewPortOffsetX = config.Get(CONFIG_SECTION_ID, "ViewPortOffsetX").ToInt32();
                    if (config.ContainsKey(CONFIG_SECTION_ID, "ViewPortOffsetY")) surfaceData.viewPortOffsetY = config.Get(CONFIG_SECTION_ID, "ViewPortOffsetY").ToInt32();
                    if (config.ContainsKey(CONFIG_SECTION_ID, "UseColors")) surfaceData.useColors = config.Get(CONFIG_SECTION_ID, "UseColors").ToBoolean();
                    if (config.ContainsKey(CONFIG_SECTION_ID, "SearchId")) searchId = config.Get(CONFIG_SECTION_ID, "SearchId").ToString();
                    if (string.IsNullOrWhiteSpace(searchId)) searchId = "*";

                    if (config.ContainsKey(CONFIG_SECTION_ID, "ExcludeIds"))
                    {
                        excludeIds.Clear();
                        var raw = config.Get(CONFIG_SECTION_ID, "ExcludeIds").ToString();
                        if (!string.IsNullOrWhiteSpace(raw))
                        {
                            foreach (var part in raw.Split(','))
                            {
                                var t = part.Trim();
                                if (t.Length >= 3 && t != "*") excludeIds.Add(t);
                            }
                        }
                    }

                    if (config.ContainsKey(CONFIG_SECTION_ID, "ShowFarmPlots")) showFarmPlots = config.Get(CONFIG_SECTION_ID, "ShowFarmPlots").ToBoolean(true);
                    if (config.ContainsKey(CONFIG_SECTION_ID, "ShowAlgaeFarms")) showAlgaeFarms = config.Get(CONFIG_SECTION_ID, "ShowAlgaeFarms").ToBoolean(true);
                    if (config.ContainsKey(CONFIG_SECTION_ID, "ShowIrrigationBlocks")) showIrrigationBlocks = config.Get(CONFIG_SECTION_ID, "ShowIrrigationBlocks").ToBoolean(true);

                    surfaceData.newLine = new Vector2(0, 30 * surfaceData.textSize);

                    if (compactMode)
                    {
                        surfaceData.textSize = 0.6f;
                        surfaceData.titleOffset = 200;
                        surfaceData.viewPortOffsetX = 10;
                        surfaceData.viewPortOffsetY = 5;
                        surfaceData.newLine = new Vector2(0, 30 * surfaceData.textSize);
                    }
                }
                else
                {
                    MyLog.Default.WriteLine("MahrianeIndustries.LCDInfo.LCDFarmingSummaryInfo: Config syntax error: " + result.ToString());
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("MahrianeIndustries.LCDInfo.LCDFarmingSummaryInfo: Exception loading config: " + e.ToString());
            }
        }

        void UpdateBlocks()
        {
            try
            {
                totalFarmPlots = 0;
                algaeFarmCount = 0;
                irrigationBlockCount = 0;

                var myCubeGrid = myTerminalBlock.CubeGrid as MyCubeGrid;
                if (myCubeGrid == null) return;

                var blocks = MahUtillities.GetBlocks(myCubeGrid, searchId, excludeIds, ref gridMass, true, false);
                if (blocks == null || blocks.Count == 0) return;

                foreach (var b in blocks)
                {
                    if (b == null) continue;
                        // Use definition Id.SubtypeName (C#6 safe) – SubtypeName property not available directly in this runtime
                        var subtype = b.BlockDefinition != null ? b.BlockDefinition.Id.SubtypeName : "";
                    var lower = subtype.ToLower();

                    // Farm plot detection (base game uses FarmBlock/FarmPlot patterns; modded we include vertical/inset variants)
                    if (lower.Contains("farmplot") || lower.Contains("farm_block") || lower.Contains("farmblock") || lower.Contains("verticalfarmplot") || lower.Contains("insetfarmplot"))
                    {
                        totalFarmPlots++;
                        continue;
                    }

                    // Algae farm detection
                    if (lower.Contains("algaefarm") || lower.Contains("algae_farm"))
                    {
                        algaeFarmCount++;
                        continue;
                    }

                    // Irrigation detection (broad match on subtype names containing irrigation)
                    if (lower.Contains("irrigation"))
                    {
                        irrigationBlockCount++;
                        continue;
                    }
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("MahrianeIndustries.LCDInfo.LCDFarmingSummaryInfo: Exception updating blocks: " + e.ToString());
            }
        }

        void Draw()
        {
            try
            {
                TryCreateSurfaceData();

                var frame = mySurface.DrawFrame();
                var viewport = new RectangleF((mySurface.TextureSize - mySurface.SurfaceSize) / 2f, mySurface.SurfaceSize);
                var position = new Vector2(surfaceData.viewPortOffsetX, surfaceData.viewPortOffsetY) + viewport.Position;

                if (surfaceData.showHeader)
                {
                    SurfaceDrawer.DrawHeader(ref frame, ref position, surfaceData, "Farming", "");
                }

                // Move down slightly after header
                if (surfaceData.showHeader) position += surfaceData.newLine * 0;

                SurfaceDrawer.DrawFarmingSummarySprite(ref frame, ref position, surfaceData,
                    showFarmPlots, showAlgaeFarms, showIrrigationBlocks,
                    totalFarmPlots, algaeFarmCount, irrigationBlockCount);

                frame.Dispose();
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("MahrianeIndustries.LCDInfo.LCDFarmingSummaryInfo: Exception drawing: " + e.ToString());
            }
        }

        public override void Dispose()
        {
        }
    }
}
