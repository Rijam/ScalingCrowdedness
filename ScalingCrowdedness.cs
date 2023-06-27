using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

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
#if TML_2023_6
			Terraria.GameContent.IL_ShopHelper.ProcessMood += ShopHelperProcessMoodEdit;
#endif
//#if TML_2022_9
			IL.Terraria.GameContent.ShopHelper.ProcessMood += ShopHelperProcessMoodEdit;
//#endif
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
#if TML_2023_6
				if (npc.townNPC && !NPCID.Sets.NoTownNPCHappiness[npc.type] && !NPCID.Sets.IsTownPet[npc.type])
#endif
//#if TML_2022_9
				if (npc.townNPC && !NPCID.Sets.IsTownPet[npc.type])
//#endif
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
			// If the number of Town NPCs loaded is <30, don't scale anything. (There are 26 vanilla Town NPCs)
			if (numOfTownNPCsLoaded < 30)
			{
				return;
			}
			// Subtract 29 to not count those.
			int countPast29 = numOfTownNPCsLoaded - 29;
			if (countPast29 > 0)
			{
				// Divide by 10 (integer division) and add 1 to find the multiple.
				// Example: 30 Town NPCs loaded gives +1. (1 / 10 = 0) + 1 = 1
				// Example: 55 Town NPCs loaded gives +3. (26 / 10 = 2) + 1 = 3
				int scalingMultiple = (countPast29 / 10) + 1;
#if DEBUG
				ModContent.GetInstance<ScalingCrowdedness>().Logger.DebugFormat("scalingMultiple is {0}", scalingMultiple);
#endif
				minimumStartCrowding += scalingMultiple;
				minimumHateCrowded += scalingMultiple;
				ModContent.GetInstance<ScalingCrowdedness>().Logger.InfoFormat("The minimum number of Town NPCs nearby to start crowding is {0}", minimumStartCrowding);
				ModContent.GetInstance<ScalingCrowdedness>().Logger.InfoFormat("The minimum number of Town NPCs nearby hate the crowd is {0}", minimumHateCrowded);
			}
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
				ModContent.GetInstance<ScalingCrowdedness>().Logger.Debug("Patch 1 unable to be applied!");
				return; // Patch unable to be applied
			}

			// Move the cursor after 3 and onto the ret op.
			c.Index++;
			// Push the ShopHelper instance onto the stack
			c.Emit(OpCodes.Ldarg_0);
			// Call a delegate using the int and Player from the stack.
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
				ModContent.GetInstance<ScalingCrowdedness>().Logger.Debug("Patch 2 unable to be applied!");
				return; // Patch unable to be applied
			}

			// Move the cursor after 3 and onto the ret op.
			c.Index++;
			// Push the ShopHelper instance onto the stack
			c.Emit(OpCodes.Ldarg_0);
			// Call a delegate using the int and Player from the stack.
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
				ModContent.GetInstance<ScalingCrowdedness>().Logger.Debug("Patch 3 unable to be applied!");
				return; // Patch unable to be applied
			}

			// Move the cursor after 6 and onto the ret op.
			c.Index++;
			// Push the ShopHelper instance onto the stack
			c.Emit(OpCodes.Ldarg_0);
			// Call a delegate using the int and Player from the stack.
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
#if TML_2023_6
				if (npc2.active && npc2.townNPC && !NPCID.Sets.NoTownNPCHappiness[npc2.type] && !WorldGen.TownManager.CanNPCsLiveWithEachOther_ShopHelper(npc, npc2))
#endif
//#if TML_2022_9
				if (npc2.active && npc2.townNPC && !WorldGen.TownManager.CanNPCsLiveWithEachOther_ShopHelper(npc, npc2))
//#endif
				{
					Vector2 npc2HomeTile = new(npc2.homeTileX, npc2.homeTileY);
					if (npc2.homeless)
					{
						npc2HomeTile = npc2.Center / 16f;
					}

					float distanceBetweenHomes = Vector2.Distance(npc1HomeTile, npc2HomeTile);
					if (distanceBetweenHomes < 25f)
					{
						list.Add(npc2);
						npcsWithinHouse++;
					}
					else if (distanceBetweenHomes < 120f)
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
#if TML_2023_6
			if (!NPCID.Sets.NoTownNPCHappiness[npc.type] && !NPCID.Sets.IsTownPet[npc.type] && Main.LocalPlayer.GetModPlayer<ScalingCrowdednessPlayer>().showNumbersWhenTalkingToNPC)
#endif
//#if TML_2022_9
			if (!NPCID.Sets.IsTownPet[npc.type] && Main.LocalPlayer.GetModPlayer<ScalingCrowdednessPlayer>().showNumbersWhenTalkingToNPC)
//#endif
			{
				GetNearbyResidentNPCs(npc, out int npcsWithinHouse, out int npcsWithinVillage);
				if (Main.netMode == NetmodeID.SinglePlayer)
				{
					Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Chat.GetChat", npcsWithinHouse, npcsWithinVillage));
				}
				else
				{
					ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Mods.ScalingCrowdedness.Chat.GetChat", npcsWithinHouse, npcsWithinVillage), Color.White);
				}
			}
		}
	}

	public class ScalingCrowdednessPlayer : ModPlayer
	{
		public bool showNumbersWhenTalkingToNPC = false;
		public bool showNumbersEnteringWorld = false;

#if TML_2023_6
		public override void OnEnterWorld()
#endif
//#if TML_2022_9
		public override void OnEnterWorld(Player player)
//#endif
		{
			// Say what the scaling thresholds are when entering the world.
			if (Main.netMode == NetmodeID.SinglePlayer && showNumbersEnteringWorld)
			{
				Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Chat.OnEnterWorld", ModContent.GetInstance<ScalingCrowdedness>().minimumStartCrowding, ModContent.GetInstance<ScalingCrowdedness>().minimumHateCrowded));
			}
			else if (showNumbersEnteringWorld)
			{
				ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Mods.ScalingCrowdedness.Chat.OnEnterWorld", ModContent.GetInstance<ScalingCrowdedness>().minimumStartCrowding, ModContent.GetInstance<ScalingCrowdedness>().minimumHateCrowded), Color.White);
			}
		}

		// Save the players' choices.
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
			
			if (args[0].ToLower() == "townnpcchat" && args.Length >= 2)
			{
				string args1 = args[1].ToLower();
				if (args1 == "true" || args1 == "enable" || args1 == "on" || args1 == "yes")
				{
					Main.LocalPlayer.GetModPlayer<ScalingCrowdednessPlayer>().showNumbersWhenTalkingToNPC = true;
					Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Commands.TownNPCChat.Enable"));
				}
				else if (args1 == "false" || args1 == "disable" || args1 == "off" || args1 == "no")
				{
					Main.LocalPlayer.GetModPlayer<ScalingCrowdednessPlayer>().showNumbersWhenTalkingToNPC = false;
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
					Main.LocalPlayer.GetModPlayer<ScalingCrowdednessPlayer>().showNumbersEnteringWorld = true;
					Main.NewText(Language.GetTextValue("Mods.ScalingCrowdedness.Commands.EnterWorld.Enable"));
				}
				else if (args1 == "false" || args1 == "disable" || args1 == "off" || args1 == "no")
				{
					Main.LocalPlayer.GetModPlayer<ScalingCrowdednessPlayer>().showNumbersEnteringWorld = false;
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
	}
}