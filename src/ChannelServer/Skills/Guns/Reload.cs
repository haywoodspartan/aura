// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network.Sending;
using Aura.Channel.Skills.Base;
using Aura.Channel.World;
using Aura.Channel.World.Entities;
using Aura.Data.Database;
using Aura.Mabi.Const;
using Aura.Mabi.Network;
using Aura.Shared.Network;
using Aura.Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura.Channel.Skills.Guns
{
	// TODO: Handle Bullet Type

	/// <summary>
	/// Reload handler
	/// </summary>
	[Skill(SkillId.Reload)]
	public class Reload : ISkillHandler, IPreparable, IReadyable, ICompletable, ICancelable
	{
		/// <summary>
		/// Bullet Count Tag for Gun
		/// </summary>
		private const string BulletCountTag = "GVBC";

		/// <summary>
		/// Bullet Min damage tag
		/// </summary>
		private const string GBAMINTag = "GBAMIN";

		/// <summary>
		/// Bullet Max damage tag
		/// </summary>
		private const string GBAMAXTag = "GBAMAX";

		/// <summary>
		/// Max Bullets for Gun
		/// </summary>
		private const string BulletsMaxTag = "BulletsMax";

		/// <summary>
		/// Prepare Reload
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		/// <returns></returns>
		public bool Prepare(Creature creature, Skill skill, Packet packet)
		{
			if (creature.RightHand == null)
				Send.SkillPrepareSilentCancel(creature, skill.Info.Id);

			var castTime = (int)skill.RankData.Var1;

			// Set Bullets Max tag if it doesn't exist
			if (!creature.RightHand.MetaData1.Has(BulletsMaxTag))
			{
				creature.RightHand.MetaData1.SetShort(BulletsMaxTag, (short)creature.RightHand.Data.BulletsMax);
				Send.ItemUpdate(creature, creature.RightHand);
			}

			// Set Bullet Count tag if it doesn't exist
			if (!creature.RightHand.MetaData1.Has(BulletCountTag))
			{
				creature.RightHand.MetaData1.SetShort(BulletCountTag, 0);
				Send.ItemUpdate(creature, creature.RightHand);
			}

			// Create GBAMIN and GBAMAX tags if they don't exist
			if (!creature.RightHand.MetaData1.Has(GBAMINTag) && !creature.RightHand.MetaData1.Has(GBAMAXTag))
			{
				creature.RightHand.MetaData1.SetShort(GBAMINTag, 0);
				creature.RightHand.MetaData1.SetShort(GBAMAXTag, 0);
				Send.ItemUpdate(creature, creature.RightHand);
			}

			Send.SkillPrepare(creature, skill.Info.Id, castTime);

			return true;
		}

		/// <summary>
		/// Readies the skill, also using it in the process.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		/// <returns></returns>
		public bool Ready(Creature creature, Skill skill, Packet packet)
		{
			// Following Packet Data for Stacks... DevCat why?
			skill.Stacks = 1;
			skill.Stacks = 0;

			// Get Bullet item in creature's inventory
			var creatureAmmo = creature.Inventory.Items.Where(item => item.HasTag("/bullet/"));
			var nearestX = creatureAmmo.Min(item => item.GetPosition().X);
			var nearestY = creatureAmmo.Min(item => item.GetPosition().Y);
			var ammoItem = creatureAmmo.Single(item => item.GetPosition().X == nearestX && item.GetPosition().Y == nearestY);
			if (ammoItem == null)
				return false;

			// Add damage based on which type of bullet was selected
			short damageAdded = 0;
			switch (ammoItem.Info.Id)
			{
				case 45036: // Training Mana Bullet
					damageAdded = 5;
					break;

				case 45037: // Mana Bullet
					damageAdded = 10;
					break;

				case 45038: // High-Density Mana Bullet
					damageAdded = 20;
					break;

				case 53564: // Suntouched Mana Bullet
					damageAdded = 30;
					break;

				default:
					damageAdded = 0;
					break;
			}


			// Get Reload Amount (Max Bullets)
			var ReloadAmount = creature.RightHand.MetaData1.GetShort(BulletsMaxTag);

			// Gun Update
			creature.RightHand.MetaData1.SetShort(BulletCountTag, ReloadAmount);
			creature.RightHand.MetaData1.SetShort(GBAMINTag, damageAdded);
			creature.RightHand.MetaData1.SetShort(GBAMAXTag, damageAdded);
			Send.ItemUpdate(creature, creature.RightHand);

			// Remove Bullet item
			creature.Inventory.Decrement(ammoItem);

			Send.UseMotion(creature, 131, 14);

			Send.SkillUse(creature, skill.Info.Id);

			return true;
		}

		/// <summary>
		/// Sends SkillComplete when the skill is finished
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public void Complete(Creature creature, Skill skill, Packet packet)
		{
			Send.SkillComplete(creature, skill.Info.Id);
		}

		/// <summary>
		/// Cancels the skill.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		public void Cancel(Creature creature, Skill skill)
		{
		}
	}
}
