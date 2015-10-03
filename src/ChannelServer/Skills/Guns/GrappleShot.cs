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
	/// Grapple Shot skill handler
	/// </summary>
	/// Bullet Use: 2
	/// Var 1: Bullet Use
	/// Var 2: Damage
	/// Var 4: Max Range
	/// Var 5: Min Range
	[Skill(SkillId.GrappleShot)]
	public class GrappleShot : ISkillHandler, IPreparable, IReadyable, IUseable, ICancelable
	{
		/// <summary>
		/// Bullet Count Tag for Gun
		/// </summary>
		private const string BulletCountTag = "GVBC";

		/// <summary>
		/// Attacker's stun after skill use
		/// </summary>
		private const int AttackerStun = 0;

		/// <summary>
		/// Target's stun after being hit
		/// </summary>
		private const int TargetStun = 2000;

		/// <summary>
		/// Knockback Distance if killed
		/// </summary>
		private const int KnockbackDistance = 150;

		/// <summary>
		/// Target's stability reduction on hit
		/// </summary>
		private const int StabilityReduction = 10;

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

			// Check Bullet Count
			var bulletCount = creature.RightHand.MetaData1.GetShort(BulletCountTag);
			if (bulletCount < skill.RankData.Var1)
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
		/// Uses Grapple Shot
		/// </summary>
		/// <param name="attacker"></param>
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
			if (attacker.GetPosition().InRange(target.GetPosition(), (int)skill.RankData.Var4)) // If attacker is within Max Range
			{
				if (!attacker.GetPosition().InRange(target.GetPosition(), (int)skill.RankData.Var5)) // If attacker isn't within Min Range
				{
					// Proceed
					goto L_Proceed;
				}
				else
				{
					// You are too close
					Send.Notice(attacker, Localization.Get("You are too close to the target."));
					Send.SkillUseSilentCancel(attacker);
					return;
				}
			}
			else
			{
				// You are too far
				Send.Notice(attacker, Localization.Get("You are too far away."));
				Send.SkillUseSilentCancel(attacker);
				return;
			}

			L_Proceed:

			// Position calculations
			attacker.StopMove();
			var attackerPos = attacker.GetPosition();
			var targetPos = target.GetPosition();
			var distanceFrom = attackerPos.GetDistance(targetPos) - 50;
			var newAttackerPos = attackerPos.GetRelative(targetPos, distanceFrom);

			// Effects to attacker
			Send.Effect(attacker, 334, (byte)1, targetEntityId, 434, 429);
			Send.EffectDelayed(attacker, 434, 334, (byte)2, 929, (float)716.1226, (float)newAttackerPos.X, (float)newAttackerPos.Y);
			Send.EffectDelayed(attacker, 863, 334, (byte)3, targetEntityId, (byte)1);
			Send.EffectDelayed(attacker, 1363, 334, (byte)4);
			Send.Effect(attacker, 329, (byte)1, (byte)0);

			// Use
			Send.SkillUse(attacker, skill.Info.Id, targetEntityId, 0, 1);
			skill.Stacks = 0;

			// Conditions
			attacker.Conditions.Activate(ConditionsD.Steadfast);
			var p = new Packet(Op.ConditionUpdate, attacker.EntityId); // Fast Move Condition - Needs extra packet entries
			p.PutLong(0).PutLong(0).PutLong(0x0100000000000000).PutLong(0x0000000100000000).PutLong(0).PutLong(0).PutInt(1).PutInt(184);
			p.PutString("CONDITION_FAST_MOVE_FACTOR: f:1.500000; CONDITION_FAST_MOVE_NO_LOCK: b: true;");
			p.PutLong(0);
			attacker.Region.Broadcast(p, attacker);

			// Move attacker to new position
			Send.ForceRunTo(attacker, newAttackerPos);

			// Effects to attacker
			Send.Effect(attacker, 16, (short)skill.Info.Id, targetEntityId, 0);
			Send.Effect(attacker, 329, (byte)0);

			// Remove Conditions
			attacker.Conditions.Deactivate(ConditionsD.Steadfast);
			var p2 = new Packet(Op.ConditionUpdate, attacker.EntityId); // Fast Move Condition - Needs extra packet entries
			p2.PutLong(0).PutLong(0).PutLong(0).PutLong(0).PutLong(0).PutLong(0).PutInt(0).PutLong(0);
			attacker.Region.Broadcast(p2, attacker);

			// Complete
			Send.SkillComplete(attacker, skill.Info.Id);

			// Effects to target
			Send.Effect(target, 298, (byte)0);
			Send.Effect(target, 298, (byte)0);

			// Prepare Combat Actions
			var cap = new CombatActionPack(attacker, skill.Info.Id);

			var aAction = new AttackerAction(CombatActionType.SpecialHit, attacker, skill.Info.Id, targetEntityId);
			aAction.Set(AttackerOptions.UseEffect | AttackerOptions.KnockBackHit1);
			aAction.PropId = targetEntityId;

			var tAction = new TargetAction(CombatActionType.TakeHit, target, attacker, SkillId.CombatMastery);
			tAction.Set(TargetOptions.Result);

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

			// Death or Knockback
			if (target.IsDead)
			{
				tAction.Set(TargetOptions.FinishingKnockDown);
				attacker.Shove(target, KnockbackDistance);
			}
			else
			{
				// Reduce Stability if not knocked down
				if (!target.IsKnockedDown)
				{
					target.Stability -= StabilityReduction;
				}

				// Knockback
				if (target.Stability < 30)
				{
					if (target.IsUnstable && target.Is(RaceStands.KnockDownable))
					{
						tAction.Set(TargetOptions.KnockDown);
						attacker.Shove(target, KnockbackDistance);
					}
					else if (target.Is(RaceStands.KnockBackable))
					{
						tAction.Set(TargetOptions.KnockBack);
						attacker.Shove(target, KnockbackDistance);
					}
				}
				tAction.Creature.Stun = tAction.Stun;
			}
			aAction.Creature.Stun = aAction.Stun;
			cap.Handle();

			// Item Update
			var bulletCount = attacker.RightHand.MetaData1.GetShort(BulletCountTag);
			bulletCount -= (short)skill.RankData.Var1; // 2 Bullets
			attacker.RightHand.MetaData1.SetShort(BulletCountTag, bulletCount);
			Send.ItemUpdate(attacker, attacker.RightHand);
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