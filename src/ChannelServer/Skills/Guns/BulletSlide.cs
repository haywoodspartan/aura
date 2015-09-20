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
	/// Bullet Slide Handler
	/// </summary>
	/// Bullet Use: 4
	/// Var1: Hit Count
	/// Var2: Damage
	/// Var3: ?
	[Skill(SkillId.BulletSlide)]
	public class BulletSlide : ISkillHandler, IPreparable, IReadyable, IUseable, ICompletable, ICancelable
	{
		/// <summary>
		/// Bullet Count Tag for Gun
		/// </summary>
		private const string BulletCountTag = "GVBC";

		/// <summary>
		/// Stun for attacker after skill use
		/// </summary>
		private const int AttackerStun = 700;

		/// <summary>
		/// Stun for target after attacker's skill use
		/// </summary>
		private const int TargetStun = 4000;

		/// <summary>
		/// Stability reduction for target
		/// </summary>
		private const int StabilityReduction = 10;

		/// <summary>
		/// Distance to Slide
		/// </summary>
		private const int SlideDistance = -700;

		/// <summary>
		/// Distance to knock back enemy if stability is low enough
		/// </summary>
		private const int KnockbackDistance = 100;
		
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

			// Client will automatically send reload if bullet count isn't enough

			// Check Bullet Count
			var bulletCount = creature.RightHand.MetaData1.GetShort(BulletCountTag);
			if (bulletCount < 4)
				Send.SkillPrepareSilentCancel(creature, skill.Info.Id);

			Send.SkillPrepare(creature, skill.Info.Id, skill.GetCastTime());

			return true;
		}

		/// <summary>
		/// Readies the skill
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
		/// Uses Bullet Slide
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public void Use(Creature attacker, Skill skill, Packet packet)
		{
			// Get Target
			var targetEntityId = packet.GetLong();
			var target = attacker.Region.GetCreature(targetEntityId);

			// Check Target
			if (target == null)
			{
				Send.Notice(attacker, Localization.Get("Invalid Target"));
				Send.SkillUseSilentCancel(attacker);
				return;
			}

			// Check Range
			var range = attacker.AttackRangeFor(target) + attacker.RightHand.Data.Range;
			if (!attacker.GetPosition().InRange(target.GetPosition(), range))
			{
				Send.Notice(attacker, Localization.Get("You are too far away."));
				Send.SkillUseSilentCancel(attacker);
				return;
			}

			attacker.StopMove();
			target.StopMove();

			// Slide
			var targetPos = target.GetPosition();
			var attackerPos = attacker.GetPosition();
			var newAttackerPos = attackerPos.GetRelative(targetPos, SlideDistance);
			Send.ForceRunTo(attacker, newAttackerPos);

			// Effects
			Send.Effect(attacker, 332, (byte)1, 2300, (float)newAttackerPos.X, (float)newAttackerPos.Y);
			Send.EffectDelayed(attacker, 233, 332, (byte)2, (float)500, 1167, (float)newAttackerPos.X, (float)newAttackerPos.Y);
			Send.EffectDelayed(attacker, 334, 338, (short)skill.Info.Id, 833, (short)4, 0, targetEntityId, 134, targetEntityId, 268, targetEntityId, 402, targetEntityId);

			var maxHits = skill.RankData.Var1; // 4 Gun Attacks
			var prevId = 0;

			for (byte i = 1; i <= maxHits; ++i)
			{
				// Prepare Combat Actions
				var cap = new CombatActionPack(attacker, skill.Info.Id);
				cap.Type = CombatActionPackType.NormalAttack;
				cap.Hit = i;
				cap.PrevId = prevId;
				prevId = cap.Id;

				var aAction = new AttackerAction(CombatActionType.SpecialHit, attacker, skill.Info.Id, targetEntityId);
				aAction.Set(AttackerOptions.Result);

				var tAction = new TargetAction(CombatActionType.TakeHit, target, attacker, SkillId.CombatMastery);
				tAction.Set(TargetOptions.Result | TargetOptions.MultiHit);

				cap.Add(aAction, tAction);

				// Damage
				var damage = (attacker.GetRndDualGunDamage() * (skill.RankData.Var2 / 100f));

				// Critical Hit
				var dgm = attacker.Skills.Get(SkillId.DualGunMastery);
				var extraCritChance = (dgm == null ? 0 : dgm.RankData.Var6);
				var critChance = attacker.GetRightCritChance(target.Protection) + extraCritChance;
				CriticalHit.Handle(attacker, critChance, ref damage, tAction);

				// Defense and Prot
				SkillHelper.HandleDefenseProtection(target, ref damage);

				// Defense
				Defense.Handle(aAction, tAction, ref damage);

				// Mana Shield
				ManaShield.Handle(target, ref damage, tAction);

				// Apply Damage
				target.TakeDamage(tAction.Damage = damage, attacker);

				// Aggro
				target.Aggro(attacker);

				// Stun Times
				tAction.Stun = TargetStun;
				aAction.Stun = AttackerStun;
				tAction.Delay = 334;

				// Death or Knockback
				if (target.IsDead)
				{
					tAction.Set(TargetOptions.FinishingKnockDown);
					attacker.Shove(target, KnockbackDistance);
				}
				else
				{
					// Knockdown
					if (!target.IsKnockedDown)
					{
						target.Stability -= StabilityReduction;
					}

					// KnockDown and KnockBack - Bullet Slide
					if (target.Stability < 40)
					{
						if (target.Stability < 10)
						{
							tAction.Set(TargetOptions.KnockDown);
						}
						else
						{
							tAction.Set(TargetOptions.KnockBack);
							aAction.Set(AttackerOptions.KnockBackHit1 | AttackerOptions.KnockBackHit2);
						}
						attacker.Shove(target, KnockbackDistance);
					}
					tAction.Creature.Stun = tAction.Stun;
				}
				aAction.Creature.Stun = aAction.Stun;
				cap.Handle();
			}

			// Item Update
			var bulletCount = attacker.RightHand.MetaData1.GetShort(BulletCountTag);
			bulletCount -= 4;
			attacker.RightHand.MetaData1.SetShort(BulletCountTag, bulletCount);
			Send.ItemUpdate(attacker, attacker.RightHand);

			skill.Stacks = 0;

			Send.SkillUse(attacker, skill.Info.Id, targetEntityId, 0, 1);
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
