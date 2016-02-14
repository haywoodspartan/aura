// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network.Sending;
using Aura.Channel.Skills.Base;
using Aura.Channel.Skills.Magic;
using Aura.Channel.Skills.Combat;
using Aura.Channel.World;
using Aura.Channel.World.Entities;
using Aura.Data.Database;
using Aura.Mabi;
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
	/// Var1: Bullet Use
	/// Var2: Damage
	/// Var4: Max Range
	/// Var5: Min Range
	[Skill(SkillId.GrappleShot)]
	public class GrappleShot : ISkillHandler, IPreparable, IReadyable, IUseable, ICancelable, IInitiableSkillHandler
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
		/// Distance to land from the target.
		/// </summary>
		/// <remarks>
		/// Grapple shot doesn't send the player directly to the target's potision,
		/// but instead to a position slightly away from it.
		/// </remarks>
		private const int LandingDistance = 100;

		/// <summary>
		/// Subscribes handlers to events required for training.
		/// </summary>
		public void Init()
		{
			ChannelServer.Instance.Events.CreatureAttackedByPlayer += this.OnCreatureAttackedByPlayer;
		}

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
			if (bulletCount < skill.RankData.Var1 && !creature.Conditions.Has(ConditionsD.WayOfTheGun))
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
			if (target == null || target.IsDead)
			{
				Send.Notice(attacker, Localization.Get("Invalid Target"));
				Send.SkillUseSilentCancel(attacker);
				attacker.Unlock(Locks.Walk | Locks.Run);
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
					attacker.Unlock(Locks.Walk | Locks.Run);
					return;
				}
			}
			else
			{
				// You are too far
				Send.Notice(attacker, Localization.Get("You are too far away."));
				Send.SkillUseSilentCancel(attacker);
				attacker.Unlock(Locks.Walk | Locks.Run);
				return;
			}

			L_Proceed:

			// Position calculations
			attacker.StopMove();
			attacker.Lock(Locks.Walk | Locks.Run);
			var attackerPos = attacker.GetPosition();
			var targetPos = target.GetPosition();
			var newAttackerPos = attackerPos.GetRelative(targetPos, (LandingDistance * -1)); // Only moves to the target until a certain distance

			// Effects to attacker
			Send.Effect(attacker, Effect.GrappleShot, (byte)1, targetEntityId, 434, 429); // Grapple Graphic Effect
			Send.EffectDelayed(attacker, 434, Effect.GrappleShot, (byte)2, 929, (float)716.1226, (float)newAttackerPos.X, (float)newAttackerPos.Y); // Grapple shooting motion
			Send.EffectDelayed(attacker, 863, Effect.GrappleShot, (byte)3, targetEntityId, (byte)1); // "Roll and Shoot" motion after grapple effect
			Send.EffectDelayed(attacker, 1363, Effect.GrappleShot, (byte)4); // ?
			Send.Effect(attacker, 329, (byte)1, (byte)0); // ?

			// Use
			Send.SkillUse(attacker, skill.Info.Id, targetEntityId, 0, 1);
			skill.Stacks = 0;

			// Conditions
			attacker.Conditions.Activate(ConditionsD.Steadfast);

			var extra = new MabiDictionary();
			extra.SetFloat("CONDITION_FAST_MOVE_FACTOR", 1.500000f);
			extra.SetBool("CONDITION_FAST_MOVE_NO_LOCK", true);
			attacker.Conditions.Activate(ConditionsC.FastMove, extra);

			// Move attacker to new position
			Send.ForceRunTo(attacker, newAttackerPos);

			// Effects to attacker
			Send.Effect(attacker, 16, (short)skill.Info.Id, targetEntityId, 0); // ?
			Send.Effect(attacker, 329, (byte)0); // ?

			// Remove Conditions
			attacker.Conditions.Deactivate(ConditionsD.Steadfast);
			attacker.Conditions.Deactivate(ConditionsC.FastMove);

			// Prepare Combat Actions
			var cap = new CombatActionPack(attacker, skill.Info.Id);

			var aAction = new AttackerAction(CombatActionType.SpecialHit, attacker, targetEntityId);
			aAction.Set(AttackerOptions.UseEffect | AttackerOptions.KnockBackHit1);
			aAction.PropId = targetEntityId;

			var tAction = new TargetAction(CombatActionType.TakeHit, target, attacker, SkillId.CombatMastery);
			tAction.Set(TargetOptions.Result);
			tAction.AttackerSkillId = skill.Info.Id;

			cap.Add(aAction, tAction);

			// Damage
			var damage = (attacker.GetRndDualGunDamage() * (skill.RankData.Var2 / 100f));

			// Master Title
			if (attacker.Titles.SelectedTitle == 10915)
				damage += (damage * (15 / 100f)); // +15% Damage

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

			// Targets can be friendly entities as well.
			if (attacker.CanTarget(target))
			{
				// Apply Damage
				target.TakeDamage(tAction.Damage = damage, attacker);

				// Aggro
				target.Aggro(attacker);

				tAction.Stun = TargetStun;
			}

			aAction.Stun = AttackerStun;

			// Death or Knockback
			if (target.IsDead)
			{
				tAction.Set(TargetOptions.FinishingKnockDown);
				attacker.Shove(target, KnockbackDistance);
				cap.Handle(); // The cap.Handle condition below doesn't apply to dead enemies.
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

			// No cap effects if friendly entity.
			if (attacker.CanTarget(target))
				cap.Handle();

			// Item Update excluding Way Of The Gun
			if (!attacker.Conditions.Has(ConditionsD.WayOfTheGun))
			{
				var bulletCount = attacker.RightHand.MetaData1.GetShort(BulletCountTag);
				bulletCount -= (short)skill.RankData.Var1; // 2 Bullets
				attacker.RightHand.MetaData1.SetShort(BulletCountTag, bulletCount);
				Send.ItemUpdate(attacker, attacker.RightHand);
			}

			// Train
			skill.Train(1); // Use the skill

			this.Complete(attacker, skill, packet);
		}

		/// <summary>
		/// Completes the skill
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public void Complete(Creature creature, Skill skill, Packet packet)
		{
			creature.Unlock(Locks.Walk | Locks.Run);
			Send.SkillComplete(creature, skill.Info.Id);
			creature.Skills.ActiveSkill = null;
		}

		/// <summary>
		/// Cancels the skill
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		public void Cancel(Creature creature, Skill skill)
		{
		}

		/// <summary>
		/// Training, called when someone attacks something.
		/// </summary>
		/// <param name="action"></param>
		public void OnCreatureAttackedByPlayer(TargetAction action)
		{
			// Note: Using the skill counts as training, so there is a training method in the Use section as well.

			// Guns use Combat Mastery as TargetAction skill, so check for AttackerAction skill
			if (action.AttackerSkillId != SkillId.GrappleShot)
				return;

			// Get skill
			var attackerSkill = action.Attacker.Skills.Get(SkillId.GrappleShot);
			if (attackerSkill == null) return; // Should be impossible.

			// Learning by attacking
			switch (attackerSkill.Info.Rank)
			{
				case SkillRank.RF:
				case SkillRank.RE:
				case SkillRank.RD:
				case SkillRank.RC:
					attackerSkill.Train(2); // Attack an enemy
					break;

				case SkillRank.RB:
				case SkillRank.RA:
				case SkillRank.R9:
				case SkillRank.R8:
				case SkillRank.R7:
				case SkillRank.R6:
				case SkillRank.R5:
				case SkillRank.R4:
				case SkillRank.R3:
				case SkillRank.R2:
				case SkillRank.R1:
					attackerSkill.Train(2); // Attack an enemy
					if (action.Has(TargetOptions.Critical)) attackerSkill.Train(3); // Critical Hit
					break;
			}
		}
	}
}