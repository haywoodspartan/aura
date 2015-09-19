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
	/// Var1: Hit Count ?
	/// Var4: Additional Min Damage
	/// Var5: Additional Max Damage
	/// Var6: Additional Critical %
	[Skill(SkillId.DualGunMastery)]
	public class DualGunMastery : ISkillHandler, IPreparable, IUseable, ICompletable, ICancelable
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
		private const int KnockbackDistance = 300; // Unofficial

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
			if (creature.RightHand == null)
				Send.SkillPrepareSilentCancel(creature, skill.Info.Id);

			// Set Bullet Count Tag if it doesn't exist
			if (!creature.RightHand.MetaData1.Has(BulletCountTag))
			{
				creature.RightHand.MetaData1.SetShort(BulletCountTag, 0);
				Send.ItemUpdate(creature, creature.RightHand);
			}

			// TODO: Reload when BulletCount < 2

			// Check Bullet Count
			var bulletCount = creature.RightHand.MetaData1.GetShort(BulletCountTag);
			if (bulletCount < 2)
				Send.SkillPrepareSilentCancel(creature, skill.Info.Id);

			Send.SkillUse(creature, skill.Info.Id);

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
				Send.Notice(attacker, Localization.Get("Invalid Target.")); // Unofficial
				Send.SkillUseSilentCancel(attacker);
				return;
			}

			var targetPos = target.GetPosition();
			var range = attacker.AttackRangeFor(target);

			// Check Range
			if (!attacker.GetPosition().InRange(targetPos, range))
			{
				Send.Notice(attacker, Localization.Get("Out of range.")); // Unofficial
				Send.SkillUseSilentCancel(attacker);
				return;
			}

			// Number of hits
			byte hitCount = (byte)skill.RankData.Var1;

			for (byte i = 1; i <= hitCount; ++i)
			{
				// Prepare Combat Actions
				var aAction = new AttackerAction(CombatActionType.RangeHit, attacker, skill.Info.Id, targetEntityId);
				aAction.Set(AttackerOptions.Result);

				var tAction = new TargetAction(CombatActionType.TakeHit, target, attacker, SkillId.CombatMastery);
				tAction.Set(TargetOptions.Result);

				var cap = new CombatActionPack(attacker, skill.Info.Id, aAction, tAction);

				// Damage
				var damage = attacker.GetRndDualGunDamage();

				// Critical Hit
				var critChance = attacker.GetRightCritChance(target.Protection);
				critChance += skill.RankData.Var6; // Not sure how to add Var 6 yet...
				CriticalHit.Handle(attacker, critChance, ref damage, tAction);

				// Subtract damage in respect to target's def/prot
				SkillHelper.HandleDefenseProtection(target, ref damage); // Is this the same for ranged skills?

				// Defense
				Defense.Handle(aAction, tAction, ref damage);

				// Mana Shield
				ManaShield.Handle(target, ref damage, tAction);

				// Apply Damage
				if (damage > 0)
					target.TakeDamage(tAction.Damage = damage, attacker);

				// Aggro
				target.Aggro(attacker);

				// Stun Times
				tAction.Stun = 0;
				aAction.Stun = 0;

				// Death or Knockback
				if (target.IsDead)
				{
					tAction.Set(TargetOptions.FinishingKnockDown);
					aAction.Stun = AfterKillStun;
					attacker.Shove(target, KnockbackDistance);
					cap.Handle();
					goto L_End;
				}
				else
				{
					if (!target.IsKnockedDown)
					{
						target.Stability -= StabilityReduction;
					}

					// Knockback
					if (target.Stability < 30)
					{
						tAction.Set(TargetOptions.KnockBack);
						attacker.Shove(target, KnockbackDistance);
					}
					tAction.Creature.Stun = tAction.Stun;
				}

				aAction.Creature.Stun = aAction.Stun;
				cap.Handle();
			}

			L_End:

			// Item Update
			var bulletCount = attacker.RightHand.MetaData1.GetShort(BulletCountTag);
			bulletCount -= 2;
			attacker.RightHand.MetaData1.SetShort(BulletCountTag, bulletCount);
			Send.ItemUpdate(attacker, attacker.RightHand);
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
		}
	}
}