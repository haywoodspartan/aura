// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network.Sending;
using Aura.Channel.Skills.Base;
using Aura.Channel.Skills.Magic;
using Aura.Channel.Skills.Combat;
using Aura.Channel.World;
using Aura.Channel.World.Entities;
using Aura.Data.Database;
using Aura.Mabi.Const;
using Aura.Mabi.Network;
using Aura.Shared.Network;
using Aura.Shared.Util;
using System.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Aura.Channel.Skills.Guns
{
	/// <summary>
	/// Way Of The Gun skill handler
	/// </summary>
	/// Var1: Duration
	/// Var2: Attack Speed
	[Skill(SkillId.WayOfTheGun)]
	public class WayOfTheGun : ISkillHandler, IPreparable, ICompletable
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
		/// Prepares the skill
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		/// <returns></returns>
		public bool Prepare(Creature creature, Skill skill, Packet packet)
		{
			if (creature.RightHand == null)
				Send.SkillPrepareSilentCancel(creature, skill.Info.Id);

			// Set Bullet Count Tag if it doesn't exist
			if (!creature.RightHand.MetaData1.Has(BulletCountTag))
			{
				creature.RightHand.MetaData1.SetShort(BulletCountTag, 0);
				Send.ItemUpdate(creature, creature.RightHand);
			}

			creature.StopMove();

			// Set Gun Ammo to 0
			creature.RightHand.MetaData1.SetShort(BulletCountTag, 0);
			Send.ItemUpdate(creature, creature.RightHand);

			// Activate WOTG Condition
			creature.Conditions.Activate(ConditionsD.WayOfTheGun);

			// Unk Packet
			var p = new Packet(0x7534, creature.EntityId);
			p.PutByte(0).PutByte(0).PutByte(0);
			creature.Region.Broadcast(p, creature);

			// Skill Use
			Send.SkillUse(creature, skill.Info.Id);
			skill.State = SkillState.Used;

			// Deactivate condition after skill duration
			Timer t = null;
			t = new Timer(_ =>
			{
				GC.KeepAlive(t);
				if (creature.Conditions.Has(ConditionsD.WayOfTheGun))
				{
					creature.Conditions.Deactivate(ConditionsD.WayOfTheGun);
					Send.MotionCancel2(creature, 0);
				}
			}, null, (int)skill.RankData.Var1, Timeout.Infinite);

			return true;
		}

		/// <summary>
		/// Sends SkillComplete
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public void Complete(Creature creature, Skill skill, Packet packet)
		{
			Send.SkillComplete(creature, skill.Info.Id);
		}
	}
}
