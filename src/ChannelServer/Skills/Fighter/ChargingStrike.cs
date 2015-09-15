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

namespace Aura.Channel.Skills.Fighter
{
	/// <summary>
	/// Charging Strike skill handler
	/// </summary>
	/// Var 1: Damage
	/// Var 2: Cooldown
	/// Var 3: Range
	[Skill(SkillId.ChargingStrike)]
	public class ChargingStrike : ISkillHandler, IPreparable, IReadyable, IUseable, ICompletable, ICancelable
	{
		/// <summary>
		/// Attacker's stun
		/// </summary>
		private const int AttackerStun = 0;

		/// <summary>
		/// Target's stun
		/// </summary>
		private const int TargetStun = 4000;

		/// <summary>
		/// Knockback Distance
		/// </summary>
		private const int KnockbackDistance = 400; // Not official, only a placeholder.

		/// <summary>
		/// Target's stability reduction on hit
		/// </summary>
		private const int StabilityReduction = 90;

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
		/// Readies skill - casting is complete.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		/// <returns></returns>
		public bool Ready(Creature creature, Skill skill, Packet packet)
		{
			skill.Stacks = 1;
			Send.SkillReady(creature, skill.Info.Id);

			return true;
		}

		/// <summary>
		/// Uses Charging Strike
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
				Send.Notice(attacker, Localization.Get("Invalid target.")); // Not official, only a placeholder.
				Send.SkillUseSilentCancel(attacker);
				return;
			}

			var targetPos = target.GetPosition();

			// Check Range
			if (!attacker.GetPosition().InRange(targetPos, (int)skill.RankData.Var3))
			{
				Send.Notice(attacker, Localization.Get("Out of range.")); // Not official, only a placeholder.
				Send.SkillUseSilentCancel(attacker);
				return;
			}

			Send.Effect(attacker, 413, 281, (byte)0, targetEntityId);
			attacker.Conditions.Activate(ConditionsC.FastMove);
			Send.ForceRunTo(attacker, targetPos);

			// Prepare Combat Actions
			var aAction = new AttackerAction(CombatActionType.SpecialHit, attacker, skill.Info.Id, targetEntityId);
			aAction.Set(AttackerOptions.Result | AttackerOptions.UseEffect);

			var tAction = new TargetAction(CombatActionType.TakeHit, target, attacker, skill.Info.Id);
			tAction.Set(TargetOptions.Result);

			var cap = new CombatActionPack(attacker, skill.Info.Id, aAction, tAction);

			// Damage
			var damage = attacker.GetRndTotalDamage() * (skill.RankData.Var1 / 100); // Not official, Will has to be taken into account.

			// Critical Hit
			var critChance = attacker.GetRightCritChance(target.Protection);
			CriticalHit.Handle(attacker, critChance, ref damage, tAction);

			// Subtract target def/prot
			SkillHelper.HandleDefenseProtection(target, ref damage);

			// Defense
			Defense.Handle(aAction, tAction, ref damage);

			// Mana Shield
			ManaShield.Handle(target, ref damage, tAction);

			// Apply damage to target
			if (damage > 0)
				target.TakeDamage(tAction.Damage = damage, attacker);

			// Aggro
			target.Aggro(attacker);

			// Stun Times
			aAction.Stun = (short)AttackerStun;
			tAction.Stun = (short)TargetStun;

			// Death or Knockback
			if (target.IsDead)
			{
				tAction.Set(TargetOptions.FinishingKnockDown);
				attacker.Shove(target, KnockbackDistance);
			}
			else
			{
				if (target.IsKnockedDown)
				{
					tAction.Stun = 0;
				}
				else
				{
					target.Stability -= StabilityReduction;
					// Note: Charging Strike doesn't knockback.
				}
				tAction.Creature.Stun = tAction.Stun;
			}

			aAction.Creature.Stun = aAction.Stun;

			// Time between charging strike effect and hit
			System.Threading.Timer t = null;
			t = new System.Threading.Timer(_ =>
			{
				GC.KeepAlive(t);
				cap.Handle();
			}, null, 500, System.Threading.Timeout.Infinite);

			Send.SkillUseEntity(attacker, skill.Info.Id, targetEntityId);
			skill.Stacks = 0;

			attacker.Conditions.Deactivate(ConditionsC.FastMove);
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

			// Chain timer should be here somewhere
		}

		/// <summary>
		/// Cancels the skill
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		public void Cancel(Creature creature, Skill skill)
		{
		}
	}
}
