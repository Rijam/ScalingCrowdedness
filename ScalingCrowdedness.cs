using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace ScalingCrowdedness
{
	public class ScalingCrowdedness : Mod
	{
		public ScalingCrowdedness Instance;
		public int numOfTownNPCsLoaded = 0;
		public int minimumStartCrowding = 3;
		public int minimumHateCrowded = 6;

		public override void PostSetupContent()
		{
			numOfTownNPCsLoaded = FindNumberOfTownNPCsLoad();
			FigureOutTheScaling();
			Terraria.GameContent.IL_ShopHelper.ProcessMood += ShopHelperProcessMoodEdit;
			Terraria.GameContent.IL_ShopHelper.GetNearbyResidentNPCs += ShopHelperGetNearbyResidentNPCs; 
		}

		/// <summary>
		/// Finds the number of Town NPCs that are loaded.
		/// </summary>
		/// <returns>The count</returns>
		public static int FindNumberOfTownNPCsLoad()
		{
			int count = 0;
			for (int i = 0; i < NPCLoader.NPCCount; i++)
			{
				NPC npc = new();
				npc.SetDefaults(i);
				if (npc.townNPC && !NPCID.Sets.NoTownNPCHappiness[npc.type] && !NPCID.Sets.IsTownPet[npc.type])
				{
					count++;
#if DEBUG
					ModContent.GetInstance<ScalingCrowdedness>().Logger.DebugFormat("Found a Town NPC with ID {0}. Name is: {1}", i, (npc.FullName == "" && npc.ModNPC != null) ? npc.ModNPC.FullName : npc.FullName);
#endif
				}
			}
			ModContent.GetInstance<ScalingCrowdedness>().Logger.DebugFormat("Found {0} Town NPCs", count);
			return count;
		}

		// Vanilla behavior:
		// >= 4 npcsWithinHouse causes prices to be multiplied by 1.05
		// 4-5 npcsWithinHouse shows the DislikeCrowded text.
		// >= 6 npcsWithinHouse shows the HateCrowded text.
		// HateCrowded doesn't actually change the price any more than DislikeCrowded, it's just to tell the player that the price has been changed a lot.

		// Scaling behavior:
		// 26 Town NPCs in vanilla. Don't start the scaling until 30+ NPCs
		// For every 10 additional Town NPCs past 30, increase the numbers by 1.

		// Example: 30 Town NPCs loaded:
		//   >= 5 npcsWithinHouse causes prices to be multiplied by 1.05
		//   6-7 npcsWithinHouse shows the DislikeCrowded text.
		//   >= 8 npcsWithinHouse shows the HateCrowded text.

		// Example: 40 Town NPCs loaded:
		//   >= 6 npcsWithinHouse causes prices to be multiplied by 1.05
		//   7-8 npcsWithinHouse shows the DislikeCrowded text.
		//   >= 9 npcsWithinHouse shows the HateCrowded text.

		// Example: 50 Town NPCs loaded:
		//   >= 7 npcsWithinHouse causes prices to be multiplied by 1.05
		//   8-9 npcsWithinHouse shows the DislikeCrowded text.
		//   >= 10 npcsWithinHouse shows the HateCrowded text.


		// Vanilla code:
		/*
			int npcsWithinHouse;
			int npcsWithinVillage;
			List<NPC> nearbyResidentNPCs = GetNearbyResidentNPCs(npc, out npcsWithinHouse, out npcsWithinVillage);
			bool flag = true;
			float num = 1.05f;
			if (npc.type == 663) {
				flag = false;
				num = 1f;
				if (npcsWithinHouse < 2 && npcsWithinVillage < 2) {
					AddHappinessReportText("HateLonely");
					_currentPriceAdjustment = 1000f;
				}
			}

			if (true && npcsWithinHouse > 3) { // Need to change this three.
				for (int i = 3; i < npcsWithinHouse; i++) { // Also need to change this three.
					_currentPriceAdjustment *= num;
				}

				if (npcsWithinHouse > 6) // Need to change this six.
					AddHappinessReportText("HateCrowded");
				else
					AddHappinessReportText("DislikeCrowded");
			}

			if (flag && npcsWithinHouse <= 2 && npcsWithinVillage < 4) {
				AddHappinessReportText("LoveSpace");
				_currentPriceAdjustment *= 0.95f;
			}
		*/

		/// <summary>
		/// Add the scaling based on how many Town NPCs are loaded.
		/// </summary>
		public void FigureOutTheScaling()
		{
			minimumStartCrowding = InformationElement.MakeTheoreticalThreshold(numOfTownNPCsLoaded, out int scalingMultiple); // Default: 4 (Minus 1 is 3)
			minimumHateCrowded = minimumStartCrowding + 3;
#if DEBUG
			ModContent.GetInstance<ScalingCrowdedness>().Logger.DebugFormat("scalingMultiple is {0}", scalingMultiple);
#endif
			ModContent.GetInstance<ScalingCrowdedness>().Logger.InfoFormat("The minimum number of Town NPCs nearby to start crowding is {0}", minimumStartCrowding);
			ModContent.GetInstance<ScalingCrowdedness>().Logger.InfoFormat("The minimum number of Town NPCs nearby hate the crowd is {0}", minimumHateCrowded);
		}

		/// <summary>
		/// Applies 3 IL edits
		/// </summary>
		/// <param name="il">IL</param>
		private static void ShopHelperProcessMoodEdit(ILContext il)
		{
			// I don't actually know that much about IL editing.

			ILCursor c = new(il);

			// Try to find where 3 is placed onto the stack
			// This 3 is the start of the crowding 
			if (!c.TryGotoNext(i => i.MatchLdcI4(3)))
			{
				ModContent.GetInstance<ScalingCrowdedness>().Logger.Debug("Patch 1 of ShopHelperProcessMoodEdit unable to be applied! ");
				return; // Patch unable to be applied
			}

			// Move the cursor after 3 and onto the ret op.
			c.Index++;
			// Push the ShopHelper instance onto the stack
			c.Emit(OpCodes.Ldarg_0);
			// Call a delegate using the int and ShopHelper from the stack.
			c.EmitDelegate<Func<int, ShopHelper, int>>((returnValue, shopHelper) => {
				// Regular c# code
				// Original code:

				// if (true && npcsWithinHouse > 3)

				// Change the 3 to the minimumStartCrowding value.

				return ModContent.GetInstance<ScalingCrowdedness>().minimumStartCrowding;
			});

			// Try to find where 3 is placed onto the stack
			// This 3 is the start of the price multiplication. We want to change this or the prices will be multiplied more than they should.
			if (!c.TryGotoNext(i => i.MatchLdcI4(3)))
			{
				ModContent.GetInstance<ScalingCrowdedness>().Logger.Debug("Patch 2 of ShopHelperProcessMoodEdit unable to be applied!");
				return; // Patch unable to be applied
			}

			// Move the cursor after 3 and onto the ret op.
			c.Index++;
			// Push the ShopHelper instance onto the stack
			c.Emit(OpCodes.Ldarg_0);
			// Call a delegate using the int and ShopHelper from the stack.
			c.EmitDelegate<Func<int, ShopHelper, int>>((returnValue, shopHelper) => {
				// Regular c# code
				// Original code:

				// for (int i = 3; i < npcsWithinHouse; i++) {
				// _currentPriceAdjustment *= num;
				// }

				// Also change the 3 to the minimumStartCrowding value.

				return ModContent.GetInstance<ScalingCrowdedness>().minimumStartCrowding;
			});

			// Try to find where 6 is placed onto the stack
			// 6 is the start of the Hate Crowded.
			if (!c.TryGotoNext(i => i.MatchLdcI4(6)))
			{
				ModContent.GetInstance<ScalingCrowdedness>().Logger.Debug("Patch 3 of ShopHelperProcessMoodEdit unable to be applied!");
				return; // Patch unable to be applied
			}

			// Move the cursor after 6 and onto the ret op.
			c.Index++;
			// Push the ShopHelper instance onto the stack
			c.Emit(OpCodes.Ldarg_0);
			// Call a delegate using the int and ShopHelper from the stack.
			c.EmitDelegate<Func<int, ShopHelper, int>>((returnValue, shopHelper) => {
				// Regular c# code
				// Original code:

				// if (npcsWithinHouse > 6)
				//		AddHappinessReportText("HateCrowded");
				// else
				//		AddHappinessReportText("DislikeCrowded");

				// Change the 6 to the minimumHateCrowded value.

				return ModContent.GetInstance<ScalingCrowdedness>().minimumHateCrowded;
			});
		}

		/// <summary>
		/// Applies 2 IL edits
		/// </summary>
		/// <param name="il">IL</param>
		private static void ShopHelperGetNearbyResidentNPCs(ILContext il)
		{
			ILCursor c = new(il);

			// Try to find where 25f is placed onto the stack
			// This 25f is the npcsWithinHouse distance
			if (!c.TryGotoNext(i => i.MatchLdcR4(25f)))
			{
				ModContent.GetInstance<ScalingCrowdedness>().Logger.Debug("Patch 1 ShopHelperGetNearbyResidentNPCs unable to be applied!");
				return; // Patch unable to be applied
			}

			// Move the cursor after 25f and onto the ret op.
			c.Index++;
			// Push the ShopHelper instance onto the stack
			c.Emit(OpCodes.Ldarg_0);
			// Call a delegate using the float and ShopHelper from the stack.
			c.EmitDelegate<Func<float, ShopHelper, float>>((returnValue, shopHelper) => {
				// Regular c# code
				// Original code:

				// if (num < 25f) {
				//		list.Add(nPC);
				//		npcsWithinHouse++;
				// }

				// Change the 25f to the TownNPCsWithinHouseRange value.

				return (float)ModContent.GetInstance<ScalingCrowdednessConfigServer>().TownNPCsWithinHouseRange;
			});

			// Try to find where 120f is placed onto the stack
			// This 120f is the npcsWithinVillage distance
			if (!c.TryGotoNext(i => i.MatchLdcR4(120f)))
			{
				ModContent.GetInstance<ScalingCrowdedness>().Logger.Debug("Patch 2 ShopHelperGetNearbyResidentNPCs unable to be applied!");
				return; // Patch unable to be applied
			}

			// Move the cursor after 120f and onto the ret op.
			c.Index++;
			// Push the ShopHelper instance onto the stack
			c.Emit(OpCodes.Ldarg_0);
			// Call a delegate using the float and ShopHelper from the stack.
			c.EmitDelegate<Func<float, ShopHelper, float>>((returnValue, shopHelper) => {
				// Regular c# code
				// Original code:

				// else if (num < 120f) {
				//		npcsWithinVillage++;
				// }

				// Change the 120f to the TownNPCsWithinVillageRange value.

				return (float)ModContent.GetInstance<ScalingCrowdednessConfigServer>().TownNPCsWithinVillageRange;
			});
		}

		// Adapted from absoluteAquarian's GraphicsLib
		public override object Call(params object[] args)
		{
			if (args is null)
				throw new ArgumentNullException(nameof(args));

			if (args[0] is not string function)
				throw new ArgumentException("Expected a function name for the first argument");

			ScalingCrowdednessConfigServer configServer = ModContent.GetInstance<ScalingCrowdednessConfigServer>();

			return function switch
			{
				"ManualAdjustmentBaseCrowdingStart" or "BaseCrowdingStart" => configServer.ManualAdjustmentBaseCrowdingStart,
				"ManualAdjustmentScalingStart" or "ScalingStart" => configServer.ManualAdjustmentScalingStart,
				"ManualAdjustmentScalingIncrements" or "ScalingIncrements" => configServer.ManualAdjustmentScalingIncrements,
				"TownNPCsWithinHouseRange" or "HousingNeighborRange" => configServer.TownNPCsWithinHouseRange,
				"TownNPCsWithinVillageRange" or "HousingVillageRange" => configServer.TownNPCsWithinVillageRange,
				"numOfTownNPCsLoaded" => numOfTownNPCsLoaded,
				"minimumStartCrowding" => minimumStartCrowding,
				"minimumHateCrowded" => minimumHateCrowded,
				_ => throw new ArgumentException($"Function \"{function}\" is not defined by ScalingCrowdedness"),
			};
		}
	}
	public class GlobalNPCs : GlobalNPC
	{
		/// <summary>
		/// Copied from vanilla.
		/// </summary>
		/// <param name="npc">NPC to check around</param>
		/// <param name="npcsWithinHouse">Out, how many Town NPCs are within in 25 tiles.</param>
		/// <param name="npcsWithinVillage">Out, how many other Town NPCs are within in 120 tiles.</param>
		/// <returns>Returns a List NPC for all NPCs within 25 tiles.</returns>
		private static List<NPC> GetNearbyResidentNPCs(NPC npc, out int npcsWithinHouse, out int npcsWithinVillage)
		{
			List<NPC> list = new();
			npcsWithinHouse = 0;
			npcsWithinVillage = 0;
			Vector2 npc1HomeTile = new(npc.homeTileX, npc.homeTileY);
			if (npc.homeless)
			{
				npc1HomeTile = new Vector2(npc.Center.X / 16f, npc.Center.Y / 16f);
			}

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				if (i == npc.whoAmI)
				{
					continue;
				}

				NPC npc2 = Main.npc[i];
				if (npc2.active && npc2.townNPC && !NPCID.Sets.NoTownNPCHappiness[npc2.type] && !WorldGen.TownManager.CanNPCsLiveWithEachOther_ShopHelper(npc, npc2))
				{
					Vector2 npc2HomeTile = new(npc2.homeTileX, npc2.homeTileY);
					if (npc2.homeless)
					{
						npc2HomeTile = npc2.Center / 16f;
					}

					float distanceBetweenHomes = Vector2.Distance(npc1HomeTile, npc2HomeTile);
					if (distanceBetweenHomes < ModContent.GetInstance<ScalingCrowdednessConfigServer>().TownNPCsWithinHouseRange) // 25f by default
					{
						list.Add(npc2);
						npcsWithinHouse++;
					}
					else if (distanceBetweenHomes < ModContent.GetInstance<ScalingCrowdednessConfigServer>().TownNPCsWithinVillageRange) // 120f by default
					{
						npcsWithinVillage++;
					}
				}
			}

			return list;
		}
		public override void GetChat(NPC npc, ref string chat)
		{
			// Say in chat how many Town NPCs are nearby.
			if (!NPCID.Sets.NoTownNPCHappiness[npc.type] && !NPCID.Sets.IsTownPet[npc.type] && ModContent.GetInstance<ScalingCrowdednessConfigClient>().ShowNumbersWhenTalkingToNPC)
			{
				GetNearbyResidentNPCs(npc, out int npcsWithinHouse, out int npcsWithinVillage);
				ScalingCrowdednessConfigServer configServer = ModContent.GetInstance<ScalingCrowdednessConfigServer>();
				if (Main.netMode == NetmodeID.SinglePlayer)
				{
					Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Chat.GetChat", npcsWithinHouse, npcsWithinVillage, configServer.TownNPCsWithinHouseRange, configServer.TownNPCsWithinVillageRange));
				}
				else
				{
					ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Mods.ScalingCrowdedness.Chat.GetChat", npcsWithinHouse, npcsWithinVillage, configServer.TownNPCsWithinHouseRange, configServer.TownNPCsWithinVillageRange), Color.White);
				}
			}
		}
	}

	public class ScalingCrowdednessPlayer : ModPlayer
	{
		//public bool showNumbersWhenTalkingToNPC = false;
		//public bool showNumbersEnteringWorld = false;

		public override void OnEnterWorld()
		{
			// Recalculate the thresholds when entering a world (if the config was changed in the main menu).
			ModContent.GetInstance<ScalingCrowdedness>()?.FigureOutTheScaling();

			// Say what the scaling thresholds are when entering the world.
			if (Main.netMode == NetmodeID.SinglePlayer && ModContent.GetInstance<ScalingCrowdednessConfigClient>().ShowNumbersEnteringWorld)
			{
				Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Chat.OnEnterWorld", ModContent.GetInstance<ScalingCrowdedness>().minimumStartCrowding, ModContent.GetInstance<ScalingCrowdedness>().minimumHateCrowded));
			}
			else if (ModContent.GetInstance<ScalingCrowdednessConfigClient>().ShowNumbersEnteringWorld)
			{
				ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Mods.ScalingCrowdedness.Chat.OnEnterWorld", ModContent.GetInstance<ScalingCrowdedness>().minimumStartCrowding, ModContent.GetInstance<ScalingCrowdedness>().minimumHateCrowded), Color.White);
			}
		}

		// Save the players' choices.
		/*
		public override void SaveData(TagCompound tag)
		{
			if (showNumbersWhenTalkingToNPC)
			{
				tag["showNumbersWhenTalkingToNPC"] = true;
			}
			if (showNumbersEnteringWorld)
			{
				tag["showNumbersEnteringWorld"] = true;
			}
		}

		public override void LoadData(TagCompound tag)
		{
			showNumbersWhenTalkingToNPC = tag.ContainsKey("showNumbersWhenTalkingToNPC");
			showNumbersEnteringWorld = tag.ContainsKey("showNumbersEnteringWorld");
		}
		*/
	}

	public class ScalingCrowdednessCommands : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "sc";
		public override string Description => Language.GetTextValue("Mods.ScalingCrowdedness.Commands.Description");

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			if (args.Length == 0)
			{
				Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Commands.Description"));
				return;
			}

			ScalingCrowdednessConfigClient configClient = ModContent.GetInstance<ScalingCrowdednessConfigClient>();

			if (args[0].ToLower() == "townnpcchat" && args.Length >= 2)
			{
				string args1 = args[1].ToLower();
				if (args1 == "true" || args1 == "enable" || args1 == "on" || args1 == "yes")
				{
					configClient.ShowNumbersWhenTalkingToNPC = true;
					ModConfigSave(configClient);
					//Main.LocalPlayer.GetModPlayer<ScalingCrowdednessPlayer>().showNumbersWhenTalkingToNPC = true;
					Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Commands.TownNPCChat.Enable"));
				}
				else if (args1 == "false" || args1 == "disable" || args1 == "off" || args1 == "no")
				{
					configClient.ShowNumbersWhenTalkingToNPC = false;
					ModConfigSave(configClient);
					//Main.LocalPlayer.GetModPlayer<ScalingCrowdednessPlayer>().showNumbersWhenTalkingToNPC = false;
					Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Commands.TownNPCChat.Disable"));
				}
				else
				{
					Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Commands.Description"));
				}
			}

			else if (args[0].ToLower() == "enterworld" && args.Length >= 2)
			{
				string args1 = args[1].ToLower();
				if (args1 == "true" || args1 == "enable" || args1 == "on" || args1 == "yes")
				{
					configClient.ShowNumbersEnteringWorld = true;
					ModConfigSave(configClient);
					//Main.LocalPlayer.GetModPlayer<ScalingCrowdednessPlayer>().showNumbersEnteringWorld = true;
					Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Commands.EnterWorld.Enable"));
				}
				else if (args1 == "false" || args1 == "disable" || args1 == "off" || args1 == "no")
				{
					configClient.ShowNumbersEnteringWorld = false;
					ModConfigSave(configClient);
					//Main.LocalPlayer.GetModPlayer<ScalingCrowdednessPlayer>().showNumbersEnteringWorld = false;
					Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Commands.EnterWorld.Disable"));
				}
				else
				{
					Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Commands.Description"));
				}
			}

			else if (args[0].ToLower() == "getthreshold")
			{
				Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Chat.OnEnterWorld", ModContent.GetInstance<ScalingCrowdedness>().minimumStartCrowding, ModContent.GetInstance<ScalingCrowdedness>().minimumHateCrowded));
			}

			else
			{
				Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Commands.Description"));
			}
		}

		/// <summary>
		/// Copied from tModLoader because it was originally internal. Maybe this is a bad idea? lol
		/// </summary>
		/// <param name="modConfig">The config instance that needs to be saved.</param>
		private static void ModConfigSave(ModConfig modConfig)
		{
			// Added for maybe more safety.
			if (modConfig is null || ConfigManager.ModConfigPath is null || ConfigManager.serializerSettings is null)
			{
				return;
			}

			Directory.CreateDirectory(ConfigManager.ModConfigPath);
			string filename = modConfig.Mod.Name + "_" + modConfig.Name + ".json";
			string path = Path.Combine(ConfigManager.ModConfigPath, filename);
			string json = JsonConvert.SerializeObject(modConfig, ConfigManager.serializerSettings);
			File.WriteAllText(path, json);
		}
	}
}