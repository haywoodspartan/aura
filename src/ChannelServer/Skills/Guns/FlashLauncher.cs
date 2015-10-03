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
	/// Flash Launcher skill handler
	/// </summary>
	/// Bullet Use: 4
	/// Var 1: Bullet Use
	/// Var 2: Damage
	/// Var 3: Damage added with Master Title (20%)
	[Skill(SkillId.FlashLauncher)]
	public class FlashLauncher : ISkillHandler, IPreparable, IReadyable, ICombatSkill, ICompletable, IInitiableSkillHandler
	{
		/// <summary>
		/// Bullet Count Tag for Gun
		/// </summary>
		private const string BulletCountTag = "GVBC";

		/// <summary>
		/// Attacker's stun upon using the skill
		/// </summary>
		private const int AttackerStun = 530;

		/// <summary>
		/// Target's stun after getting hit
		/// </summary>
		private const int TargetStun = 3500;

		/// <summary>
		/// Distance target gets knocked back
		/// </summary>
		private const int KnockbackDistance = 150;

		/// <summary>
		/// Target's stability reduction
		/// </summary>
		private const int StabilityReduction = 10;

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

		public CombatSkillResult Use(Creature attacker, Skill skill, long targetEntityId)
		{
			// Get target
			var target = attacker.Region.GetCreature(targetEntityId);

			// Check target
			if (target == null)
				return CombatSkillResult.InvalidTarget;

			var targetPos = target.GetPosition();
			var range = attacker.AttackRangeFor(target);

			// Check range
			if (!attacker.GetPosition().InRange(targetPos, range))
			{
				Send.Notice(attacker, Localization.Get("You are too far away."));
				return CombatSkillResult.OutOfRange;
			}

			attacker.StopMove();
			target.StopMove();

			// Effects
			Send.Effect(attacker, 329, (byte)1, (byte)1, 1400);

			// Insert Condition Here

			// Prepare combat actions
			var cap = new CombatActionPack(attacker, skill.Info.Id);

			var aAction = new AttackerAction(CombatActionType.RangeHit, attacker, skill.Info.Id, targetEntityId);
			aAction.Set(AttackerOptions.Result | AttackerOptions.KnockBackHit1 | AttackerOptions.KnockBackHit2);

			var tAction = new TargetAction(CombatActionType.TakeHit, target, attacker, SkillId.CombatMastery);
			tAction.Set(TargetOptions.Result);

			cap.Add(aAction, tAction);

			// Damage
			var damage = (attacker.GetRndDualGunDamage() * (skill.RankData.Var2 / 100f));

			// Master Title
			if (attacker.Titles.SelectedTitle == 10914)
				damage += (damage * (skill.RankData.Var3 / 100f)); // +20% damage

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

			// Death or knockback
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

				// Always knock down
				if (target.Is(RaceStands.KnockDownable))
				{
					tAction.Set(TargetOptions.KnockDown);
					attacker.Shove(target, KnockbackDistance);
				}
				tAction.Creature.Stun = tAction.Stun;
			}
			aAction.Creature.Stun = aAction.Stun;
			cap.Handle();

			// Effects to target
			Send.Effect(target, 298, (byte)0);
			Send.Effect(target, 298, (byte)0);

		}

		/// <summary>
		/// Training, called when someone attacks something.
		/// </summary>
		/// <param name="action"></param>
		public void OnCreatureAttackedByPlayer(TargetAction action)
		{
			// Guns use Combat Mastery as TargetAction skill
			if (action.SkillId != SkillId.CombatMastery)
				return;

			// Get skill
			var attackerSkill = action.Attacker.Skills.Get(SkillId.FlashLauncher);
			if (attackerSkill == null) return; // Should be impossible.

			// Learning by attacking
			switch (attackerSkill.Info.Rank)
			{
				case SkillRank.RF:
				case SkillRank.RE:
				case SkillRank.RD:
				case SkillRank.RC:
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
					break;
			}
		}
	}
}
