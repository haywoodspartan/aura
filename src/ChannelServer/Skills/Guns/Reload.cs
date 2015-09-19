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
		/// Bullet Count Tag for Item
		/// </summary>
		private const string BulletCountTag = "GVBC";

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

			// Set Bullets Max Tag
			if (!creature.RightHand.MetaData1.Has(BulletsMaxTag))
			{
				creature.RightHand.MetaData1.SetShort(BulletsMaxTag, (short)creature.RightHand.Data.BulletsMax);
				Send.ItemUpdate(creature, creature.RightHand);
			}

			// Set BulletCountTag
			if (!creature.RightHand.MetaData1.Has(BulletCountTag))
			{
				creature.RightHand.MetaData1.SetShort(BulletCountTag, 2);
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

			// Get Reload Amount (Max Bullets)
			var ReloadAmount = creature.RightHand.MetaData1.GetShort(BulletsMaxTag);

			// Gun Update
			creature.RightHand.MetaData1.SetShort(BulletCountTag, ReloadAmount);
			Send.ItemUpdate(creature, creature.RightHand);

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
