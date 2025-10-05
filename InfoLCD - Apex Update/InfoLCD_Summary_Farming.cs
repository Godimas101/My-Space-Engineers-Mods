using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems.TextSurfaceScripts;
using Sandbox.ModAPI;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems.TextSurfaceScripts;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Utils;
using VRageMath;

namespace SG.LCDInfo
{
	[MyTextSurfaceScript("LCDInfoScreenFarmingSummary", "$IOS LCD - Farming")]
	public class LCDFarmingSummary : MyTextSurfaceScriptBase
	{
		public LCDFarmingSummary(IMyTextSurface surface, IMyCubeBlock block, Vector2 size) : base(surface, block, size)
		{
			TryCreateSurfaceData();
			CreateConfig();
		}
		private MyIni config = new MyIni();
		public static string CONFIG_SECTION_ID = "SettingsFarmingStatus";

		// Initialize settings
		private string searchId = "*";
		private bool showSubgrids = true;
		private SurfaceDrawer.SurfaceData surfaceData;
		private bool compactMode = false;
		private List<IMyFarm> farms = new List<IMyFarm>();
		private List<IMyFunctionalBlock> verticalFarms = new List<IMyFunctionalBlock>(); // For vertical / inset farm plots
		private List<IMyFunctionalBlock> algaeFarms = new List<IMyFunctionalBlock>(); // Optional separate algae farm listing
		private double gridMass = 0;
		// Recognized additional farm plot subtype IDs from Vertical Farm Plot mod (keep unified list for future expansion)
		private static readonly HashSet<string> VerticalFarmSubtypeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"VerticalFarmPlot",
			"InsetFarmPlot"
		};

		// Exclusion: blocks that should NOT be counted as farm plots (oxygen farms only)
		private static bool IsExcludedFarmSubtype(string subtype)
		{
			if (string.IsNullOrEmpty(subtype)) return false;
			// Only exclude oxygen farm variants
			if (subtype.IndexOf("oxygenfarm", StringComparison.OrdinalIgnoreCase) >= 0) return true;
			return false;
		}

		public override void Init(MyTextSurface surface, int fontLineSpacing)
		{
			base.Init(surface, fontLineSpacing);
			MySpriteBatch.AddBackgroundTexture();
			TryCreateSurfaceData();
			CreateConfig();
			UpdateBlocks();
		}

		public override void Update()
		{
			base.Update();
			UpdateBlocks();
		}

		private void TryCreateSurfaceData()
		{
			if (surfaceData != null)
				return;

			compactMode = mySurface.SurfaceSize.X / mySurface.SurfaceSize.Y > 4;
			var textSize = compactMode ? 0.45f : 0.35f;

			// Initialize surface settings - matching Production.cs style
			surfaceData = new SurfaceDrawer.SurfaceData
			{
				surface = mySurface,
				textSize = textSize,
				titleOffset = 104,
				ratioOffset = 104,
				viewPortOffsetX = 10,
				viewPortOffsetY = 10,
				newLine = new Vector2(0, 30 * textSize),
				showHeader = true,
				showSummary = true,
				showMissing = false,
				showRatio = true,
				showBars = true,
				showSubgrids = true,
				showDocked = false,
				useColors = true
			};
		}

		private void CreateConfig()
		{
			TryCreateSurfaceData();
			config.Clear();
			config.AddSection(CONFIG_SECTION_ID);
			config.Set(CONFIG_SECTION_ID, "SearchId", $"{searchId}");
			config.Set(CONFIG_SECTION_ID, "ExcludeIds", $"{(excludeIds != null && excludeIds.Count > 0 ? String.Join(", ", excludeIds.ToArray()) : "")}"
			);
			config.Set(CONFIG_SECTION_ID, "TextSize", $"{surfaceData.textSize}");
			config.Set(CONFIG_SECTION_ID, "ViewPortOffsetX", $"{surfaceData.viewPortOffsetX}");
			config.Set(CONFIG_SECTION_ID, "ViewPortOffsetY", $"{surfaceData.viewPortOffsetY}");
			config.Set(CONFIG_SECTION_ID, "TitleFieldWidth", $"{surfaceData.titleOffset}");
			config.Set(CONFIG_SECTION_ID, "RatioFieldWidth", $"{surfaceData.ratioOffset}");
			config.Set(CONFIG_SECTION_ID, "ShowHeader", $"{surfaceData.showHeader}");
			config.Set(CONFIG_SECTION_ID, "ShowSummary", $"{surfaceData.showSummary}");
			config.Set(CONFIG_SECTION_ID, "ShowSubgrids", $"{surfaceData.showSubgrids}");
			config.Set(CONFIG_SECTION_ID, "ShowAlgaeFarms", "true");
			// Removed ShowVerticalFarms toggle (always on) to simplify configuration

			if (!config.SaveToTextSurface(mySurface, 0))
				MyLog.Default.WriteLine($"SG.LCDInfo.LCDFarmingSummary: Failed to save config to surface");
		}

		private void UpdateBlocks()
		{
			try
			{
				var myCubeGrid = mySurface.CubeGrid;
				if (myCubeGrid == null)
					return;

				IMyCubeGrid cubeGrid = myCubeGrid as IMyCubeGrid;
				bool isStation = cubeGrid.IsStatic;
				string gridId = cubeGrid.CustomName;

				// Load config for subgrids
				config.GetSection(CONFIG_SECTION_ID);
				surfaceData.showSubgrids = config.Get(CONFIG_SECTION_ID, "ShowSubgrids").ToBoolean(true);

				var myFatBlocks = SGUtillities.GetBlocks(myCubeGrid, searchId, excludeIds, ref gridMass, surfaceData.showSubgrids);
				farms.Clear();
				verticalFarms.Clear();
				algaeFarms.Clear();
				bool showAlgaeFarms = config.Get(CONFIG_SECTION_ID, "ShowAlgaeFarms").ToBoolean(true);

				foreach (var myBlock in myFatBlocks)
				{
					if (myBlock is IMyFarm farm)
					{
						// Some future mod might mis-tag oxygen/algae farms; guard regardless
						var subtypeCheck = myBlock.BlockDefinition.Id.SubtypeId.ToString();
						if (IsExcludedFarmSubtype(subtypeCheck))
							continue;
						if (subtypeCheck.IndexOf("algaefarm", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							if (showAlgaeFarms)
								algaeFarms.Add(myBlock as IMyFunctionalBlock);
							continue; // do not include in main farm list
						}
						farms.Add(farm);
						continue;
					}

					if (myBlock is IMyFunctionalBlock functionalBlock)
					{
						var subtype = myBlock.BlockDefinition.Id.SubtypeId.ToString();
						if (IsExcludedFarmSubtype(subtype))
							continue;
						if (subtype.IndexOf("algaefarm", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							if (showAlgaeFarms)
								algaeFarms.Add(functionalBlock);
							continue;
						}
						if (VerticalFarmSubtypeIds.Contains(subtype))
							verticalFarms.Add(functionalBlock);
					}
				}
			}
			catch (Exception e)
			{
				MyLog.Default.WriteLine($"SG.LCDInfo.LCDFarmingSummary: Caught Exception while updating blocks: {e.ToString()}");
			}
		}

		public override void Draw(ref MySpriteDrawFrame frame, MySpriteBatchSpriteDrawState state, Vector2? myPosition)
		{
			try
			{
				var position = new Vector2(surfaceData.viewPortOffsetX, surfaceData.viewPortOffsetY);
				if (surfaceData.showHeader)
					SurfaceDrawer.DrawHeader(ref frame, ref position, surfaceData, $"Farming [{(searchId == "*" ? "All" : searchId)} -{excludeIds.Count}]");

				DrawFarmingSummarySprite(ref frame, ref position);
			}
			catch (Exception e)
			{
				MyLog.Default.WriteLine($"SG.LCDInfo.LCDFarmingSummary: Caught Exception while DrawMainSprite: {e.ToString()}");
			}
		}

		private void DrawFarmingSummarySprite(ref MySpriteDrawFrame frame, ref Vector2 position)
		{
			try
			{
				int totalFarms = farms.Count + verticalFarms.Count;
				int workingFarms = farms.Count(f => f.IsWorking) + verticalFarms.Count(v => v.IsWorking);
				int empty = 0, growing = 0, ready = 0;
				foreach (var farm in farms)
				{
					if (!farm.IsWorking) continue;
					var info = farm.DetailedInfo;
					if (info.Contains("No Seed")) empty++; else if (info.Contains("Growing") || info.Contains("Progress:")) growing++; else if (info.Contains("Ready") || info.Contains("Harvest")) ready++;
				}
				foreach (var vf in verticalFarms)
				{
					if (!vf.IsWorking) continue;
					var info = vf.DetailedInfo;
					if (info.Contains("No Seed") || info.Contains("Empty")) empty++; else if (info.Contains("Growing") || info.Contains("Progress")) growing++; else if (info.Contains("Ready") || info.Contains("Harvest")) ready++;
				}
				bool showAlgae = config.Get(CONFIG_SECTION_ID, "ShowAlgaeFarms").ToBoolean(true);
				SurfaceDrawer.DrawFarmingSummarySprite(ref frame, ref position, surfaceData, totalFarms, workingFarms, empty, growing, ready, algaeFarms.Count, showAlgae);
			}
			catch (Exception e)
			{ MyLog.Default.WriteLine($"SG.LCDInfo.LCDFarmingSummary: Caught Exception while DrawFarmingSummarySprite: {e}"); }
		}

		// Ensure the Farming summary updates on the same tick cadence as other summaries
		public override ScriptUpdate NeedsUpdate => ScriptUpdate.Update100; // match cadence used by most other summary scripts
	}
}
