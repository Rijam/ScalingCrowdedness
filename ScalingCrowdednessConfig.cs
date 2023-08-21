using Microsoft.Xna.Framework.Graphics;
using System;
using System.ComponentModel;
using Terraria.GameContent;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Microsoft.Xna.Framework;
using Terraria.Localization;
using Terraria.UI.Chat;
using Terraria.ID;

namespace ScalingCrowdedness
{
	public class ScalingCrowdednessConfigClient : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		[DefaultValue(false)]
		public bool ShowNumbersWhenTalkingToNPC { get; set; }

		[DefaultValue(false)]
		public bool ShowNumbersEnteringWorld { get; set; }

		[CustomModConfigItem(typeof(ClickForNextConfigClient))]
		public bool NextConfigClient = new();
	}

	public class ScalingCrowdednessConfigServer : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;

		[CustomModConfigItem(typeof(InformationElement))]
		public bool ScalingInfo = new();

		[DefaultValue(4)]
		[Range(1, 11)]
		[Slider]
		public int ManualAdjustmentBaseCrowdingStart { get; set; }

		[DefaultValue(30)]
		[Range(5, 100)]
		[Slider]
		public int ManualAdjustmentScalingStart { get; set; }

		[DefaultValue(10)]
		[Range(5, 20)]
		[Slider]
		public int ManualAdjustmentScalingIncrements { get; set; }

		[DefaultValue(25)]
		[Range(1, 500)]
		[Increment(5)]
		[Slider]
		public int TownNPCsWithinHouseRange { get; set; }

		[DefaultValue(120)]
		[Range(2, 1000)]
		[Increment(5)]
		[Slider]
		public int TownNPCsWithinVillageRange { get; set; }

		[CustomModConfigItem(typeof(CalculationInformation))]
		public bool CalculationInfo = new();

		public override void OnChanged()
		{
			// If the config was changed, recalculate the thresholds.
			// Don't recalculate if in the main menu (causes problems during mod loading).
			// But then it also doesn't update if you change the config in the main menu.
			// ScalingCrowdednessPlayer.OnEnterWorld() also recalculates the thresholds to solve that.
			if (!Main.gameMenu)
			{
				ModContent.GetInstance<ScalingCrowdedness>()?.FigureOutTheScaling();
			}
		}

		/* Not written by Rijam*/
		public static bool IsPlayerLocalServerOwner(int whoAmI)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				return Netplay.Connection.Socket.GetRemoteAddress().IsLocalHost();
			}

			for (int i = 0; i < Main.maxPlayers; i++)
			{
				RemoteClient client = Netplay.Clients[i];
				if (client.State == 10 && i == whoAmI && client.Socket.GetRemoteAddress().IsLocalHost())
				{
					return true;
				}
			}
			return false;
		}

		public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref string message)
		{
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				return true;
			}

			if (!IsPlayerLocalServerOwner(whoAmI))
			{
				message = Language.GetTextValue("Mods.ScalingCrowdedness.Configs.ScalingCrowdednessConfigServer.MultiplayerMessage");
				return false;
			}
			return base.AcceptClientChanges(pendingConfig, whoAmI, ref message);
		}
		/* */
	}

	// This custom config UI element uses vanilla config elements paired with custom drawing.
	class InformationElement : ConfigElement
	{
		public override void OnBind()
		{
			base.OnBind();
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);
			var hitbox = GetInnerDimensions().ToRectangle();
			//spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(hitbox.X, hitbox.Y, 1, 30), Color.White);
			int numOfTownNPCsLoaded = ModContent.GetInstance<ScalingCrowdedness>().numOfTownNPCsLoaded;
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch,
				FontAssets.ItemStack.Value,
				Language.GetTextValue("Mods.ScalingCrowdedness.Configs.ScalingCrowdednessConfigServer.ScalingInfo.String", numOfTownNPCsLoaded, MakeTheoreticalThreshold(numOfTownNPCsLoaded, out _)),
				new Vector2(hitbox.X + 5, hitbox.Y + (hitbox.Height / 4f)),
				Color.White, 0f, Vector2.Zero, new Vector2(1f, 1f));
			//spriteBatch.DrawString(FontAssets.ItemStack.Value, Language.GetTextValue("Mods.ScalingCrowdedness.Configs.ScalingCrowdednessConfigServer.ScalingInfo.String", numOfTownNPCsLoaded, MakeTheoreticalThreshold(numOfTownNPCsLoaded)), new Vector2(hitbox.X + 5, hitbox.Y + (hitbox.Height / 4f)), Color.White);
		}

		public static int MakeTheoreticalThreshold(int numOfTownNPCsLoaded, out int scalingMultiple)
		{
			ScalingCrowdednessConfigServer configServer = ModContent.GetInstance<ScalingCrowdednessConfigServer>();
			scalingMultiple = 0;
			int minimumStartCrowding = configServer.ManualAdjustmentBaseCrowdingStart - 1; // Default: 4 (Minus 1 is 3)

			// If the number of Town NPCs loaded is <30, don't scale anything. (There are 26 vanilla Town NPCs)
			int scalingStart = configServer.ManualAdjustmentScalingStart; // Default: 30
			if (numOfTownNPCsLoaded < scalingStart)
			{
				return minimumStartCrowding;
			}
			// Subtract 29 to not count those.
			int countPastScalingStart = numOfTownNPCsLoaded - (scalingStart - 1);
			if (countPastScalingStart > 0)
			{
				// Divide by 10 (integer division) and add 1 to find the multiple.
				// Example: 30 Town NPCs loaded gives +1. (1 / 10 = 0) + 1 = 1
				// Example: 55 Town NPCs loaded gives +3. (26 / 10 = 2) + 1 = 3
				int scalingIncrements = configServer.ManualAdjustmentScalingIncrements; // Default: 10
				scalingMultiple = (countPastScalingStart / scalingIncrements) + 1;
				minimumStartCrowding += scalingMultiple;
			}
			return minimumStartCrowding;
		}
	}

	public class ClickForNextConfigClient : ConfigElement
	{

	}
	public class CalculationInformation : ConfigElement
	{

	}
}