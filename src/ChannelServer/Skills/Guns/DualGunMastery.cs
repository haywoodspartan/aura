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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura.Channel.Skills.Guns
{
	/// <summary>
	/// Dual Gun Mastery skill handler
	/// </summary>
	[Skill(SkillId.DualGunMastery)]
	public class DualGunMastery : ISkillHandler, IPreparable, IReadyable, IUseable, ICompletable, ICancelable
	{
		/// <summary>
		/// Bullet Count Tag for Item
		/// </summary>
		private const string BulletCountTag = "GVBC";

		/// <summary>
		/// Attacker's stun after a kill
		/// </summary>
		private const int AfterKillStun = 850;

		/// <summary>
		/// Knockback Distance
		/// </summary>
		private const int KnockbackDistance = 400; // Unofficial

		/// <summary>
		/// Target's stability reduction on hit
		/// </summary>
		private const int StabilityReduction = 10;

		/// <summary>
		/// Prepares skill
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		/// <returns></returns>
		public bool Prepare(Creature creature, Skill skill, Packet packet)
		{
			Send.SkillPrepare(creature, skill.Info.Id, skill.GetCastTime());

			return true;
		}

		/// <summary>
		/// Readies skill
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		/// <returns></returns>
		public bool Ready(Creature creature, Skill skill, Packet packet)
		{
			skill.Stacks = 2;
			Send.SkillReady(creature, skill.Info.Id);

			return true;
		}

		/// <summary>
		/// Uses Gun Attack
		/// </summary>
		/// <param name="attacker"></param>
		/// <param name="skill"></param>
		/// <param name="targetEntityId"></param>
		public void Use(Creature attacker, Skill skill, Packet packet)
		{
			// Get Target
			var targetEntityId = packet.GetLong();
			var target = attacker.Region.GetCreature(targetEntityId);

			// Check Target
			if (target == null)
			{
				Send.Notice(attacker, Localization.Get("Invalid target.")); // Unofficial
				Send.SkillUseSilentCancel(attacker);
				return;
			}

			var targetPos = target.GetPosition();

			// Check Range
			if (!attacker.GetPosition().InRange(targetPos, (int)skill.RankData.Var3))
			{
				Send.Notice(attacker, Localization.Get("Out of range.")); // Unofficial
				Send.SkillUseSilentCancel(attacker);
				return;
			}
		}

		/// <summary>
		/// Completes the skill
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public void Complete(Creature creature, Skill skill, Packet packet)
		{
			Send.SkillComplete(creature, skill.Info.Id);
		}

		/// <summary>
		/// Cancels the skill
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		public void Cancel(Creature creature, Skill skill)
		{
			skill.Stacks = 0;
		}
	}
}