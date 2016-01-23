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

namespace Aura.Channel.Skills.Guns
{
	/// <summary>
	/// Bullet Storm skill handler
	/// </summary>
	/// Var1: Initial Bullet Use
	/// Var2: Damage Per Shot
	/// Var5: Max Targets
	/// Var6: Attack Spread (Width)
	/// Var7: Attack Range (Length)
	/// Var8: Maximum Damage
	[Skill(SkillId.BulletStorm)]
	public class BulletStorm : ISkillHandler, IPreparable, IReadyable, IUseable, ICancelable, IInitiableSkillHandler
	{
		/// <summary>
		/// Bullet Count Tag for Gun
		/// </summary>
		private const string BulletCountTag = "GVBC";

		/// <summary>
		/// Max Bullets for Gun
		/// </summary>
		private const string BulletsMaxTag = "BulletsMax";

		/// <summary>
		/// Attacker's stun upon using the skill
		/// </summary>
		private const int AttackerStun = 0;

		/// <summary>
		/// Target's stun after getting hit
		/// </summary>
		private const int TargetStun = 4000;

		/// <summary>
		/// Distance target gets knocked back
		/// </summary>
		private const int KnockbackDistance = 200;

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
			ChannelServer.Instance.Events.CreatureAttacks += this.OnCreatureAttacks;
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

			creature.StopMove();

			Send.UseMotion(creature, 131, 2, false, false);

			/* Locks -----------------------
			Walk|Run|PickUpAndDrop|TalkToNpc
			----------------------------- */
			creature.Lock(Locks.Walk | Locks.Run | Locks.PickUpAndDrop | Locks.TalkToNpc);

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
			/* Locks -----------------------
			ChangeStance|Walk|Run|PickUpAndDrop|TalkToNpc
			----------------------------- */
			creature.Lock(Locks.ChanceStance | Locks.Walk | Locks.Run | Locks.PickUpAndDrop | Locks.TalkToNpc);

			skill.Stacks = 1;
			Send.SkillReady(creature, skill.Info.Id);

			return true;
		}

		/// <summary>
		/// Uses the skill
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public void Use(Creature attacker, Skill skill, Packet packet)
		{
			/* Locks -----------------------
			Walk|Run
			----------------------------- */
			attacker.Lock(Locks.Walk | Locks.Run);

			var targetAreaId = packet.GetLong();
			var targetAreaLoc = new Location(targetAreaId);
			var targetAreaPos = new Position(targetAreaLoc.X, targetAreaLoc.Y);

			var attackerPos = attacker.GetPosition();

			Send.SkillUse(attacker, skill.Info.Id, targetAreaId, 0, 1);
			skill.Stacks = 0;

			// Distance & Radius
			var distance = skill.RankData.Var7;
			var radius = skill.RankData.Var6;

			var attackerToTargetPointDist = attackerPos.GetDistance(targetAreaPos);
			var newTargetPosition = attackerPos.GetRelative(targetAreaPos, (int)(distance - attackerToTargetPointDist)); // Get position for use of true rectangle distance

			attacker.StopMove(); // Unnecessary at the moment, but who knows.

			// Center Points Calculation
			var attackerPoint = new Point(attackerPos.X, attackerPos.Y);
			var targetPoint = new Point(newTargetPosition.X, newTargetPosition.Y);

			var pointDist = Math.Sqrt((distance * distance) + (radius * radius)); // Pythagorean Theorem - Distance between point and opposite side's center.
			var rotationAngle = Math.Asin(radius / pointDist);

			// Calculate Points 1 & 2
			var posTemp1 = attackerPos.GetRelative(newTargetPosition, (int)(pointDist - distance));
			var pointTemp1 = new Point(posTemp1.X, posTemp1.Y);
			var p1 = this.RotatePoint(pointTemp1, attackerPoint, rotationAngle);
			var p2 = this.RotatePoint(pointTemp1, attackerPoint, (rotationAngle * -1));

			// Calculate Points 3 & 4
			var posTemp2 = newTargetPosition.GetRelative(attackerPos, (int)(pointDist - distance));
			var pointTemp2 = new Point(posTemp2.X, posTemp2.Y);
			var p3 = this.RotatePoint(pointTemp2, targetPoint, rotationAngle);
			var p4 = this.RotatePoint(pointTemp2, targetPoint, (rotationAngle * -1));

			// Prepare attacker action
			var cap = new CombatActionPack(attacker, skill.Info.Id);

			var aAction = new AttackerAction(CombatActionType.SpecialHit, attacker, targetAreaId);
			aAction.Set(AttackerOptions.UseEffect | AttackerOptions.KnockBackHit1);
			cap.Add(aAction);

			aAction.Stun = AttackerStun;

			// Get targets in descending order for effect packet using bulletDist
			var targets = attacker.Region.GetCreaturesInPolygon(p1, p2, p3, p4).Where(x => attacker.CanTarget(x)).OrderByDescending(x => x.GetPosition().GetDistance(attackerPos)).Reverse().ToList();

			// Filter by max targets [var5] and bullet count
			var bulletCount = attacker.RightHand.MetaData1.GetShort(BulletCountTag);

			if (attacker.Conditions.Has(ConditionsD.WayOfTheGun))
				bulletCount = attacker.RightHand.MetaData1.GetShort(BulletsMaxTag);

			var maxTargetsBulletCount = (int)Math.Floor((decimal)(bulletCount / 4));

			if (maxTargetsBulletCount < skill.RankData.Var5)
			{
				targets = targets.Take(maxTargetsBulletCount).ToList();
			}
			else if (maxTargetsBulletCount >= skill.RankData.Var5)
			{
				targets = targets.Take((int)skill.RankData.Var5).ToList();
			}

			// Effects & Etc
			var shootEffect = new Packet(Op.Effect, attacker.EntityId);
			shootEffect.PutInt(Effect.BulletTrail).PutShort((short)skill.Info.Id).PutInt(95).PutShort((short)targets.Count);
			var bulletDist = -380;
			var totalDelay = 0;

			// Following the pattern of effect ids, may be bullet storm motion... But it doesn't work?
			// Also changed parameter count between two logs of bullet storm. Is this because of the target count?
			Send.Effect(attacker, 335, (byte)1, (byte)1, (short)131, (short)3, 1140, (byte)2, (short)433, (short)1133); // ?

			Send.UseMotion(attacker, 131, 3, true, false); // Bullet Storm shooting motion

			// Prepare target actions
			foreach (var target in targets)
			{
				var tAction = new TargetAction(CombatActionType.TakeHit, target, attacker, SkillId.CombatMastery);
				tAction.Set(TargetOptions.Result | TargetOptions.MultiHit);
				tAction.AttackerSkillId = skill.Info.Id;
				tAction.MultiHitDamageCount = 4;
				tAction.MultiHitDamageShowTime = 95;
				tAction.MultiHitUnk1 = 0;
				tAction.MultiHitUnk2 = 421075482;
				cap.Add(tAction);

				/// Damage is calculated as (Skill Rank Damage per Target [var2 (for 4 bullets?)] * Number of Targets)
				/// However, if the target count >= 5, damage is set to max damage [var8].
				var damage = 0f;
				if (targets.Count >= 5)
				{
					damage = (attacker.GetRndDualGunDamage() * (skill.RankData.Var8 / 100f));
				}
				else
				{
					damage = (attacker.GetRndDualGunDamage() * (skill.RankData.Var2 / 100f)) * targets.Count; // Is 4 bullets needed? Damage Per Shot can be taken in many ways...
				}

				// Master Title
				if (attacker.Titles.SelectedTitle == 10918)
				{
					if (targets.Count >= 5)
						damage = (attacker.GetRndDualGunDamage() * ((skill.RankData.Var8 + 15) / 100f)); // +15% Max Damage
				}

				// Critical Hit
				var dgm = attacker.Skills.Get(SkillId.DualGunMastery);
				var extraCritChance = (dgm == null ? 0 : dgm.RankData.Var6);
				var critchance = attacker.GetRightCritChance(target.Protection) + extraCritChance;
				CriticalHit.Handle(attacker, critchance, ref damage, tAction);

				// Defense and Prot
				SkillHelper.HandleDefenseProtection(target, ref damage);

				// Defense (Skill)
				Defense.Handle(aAction, tAction, ref damage);

				// Mana Shield
				ManaShield.Handle(target, ref damage, tAction);

				// Apply Damage
				target.TakeDamage(tAction.Damage = damage, attacker);

				// Aggro
				target.Aggro(attacker);

				// Stun Time
				tAction.Stun = TargetStun;

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

					// Knock down if needed
					if (target.IsUnstable)
					{
						if (target.Is(RaceStands.KnockDownable))
						{
							tAction.Set(TargetOptions.KnockDown);
							attacker.Shove(target, KnockbackDistance);
						}
					}
					else if (target.Is(RaceStands.KnockBackable)) // Always knock back
					{
						tAction.Set(TargetOptions.KnockBack);
						attacker.Shove(target, KnockbackDistance);
					}
					tAction.Creature.Stun = tAction.Stun;
				}

				bulletDist += 380;
				totalDelay += 380;
				tAction.Delay = bulletDist;
				shootEffect.PutInt(bulletDist).PutLong(target.EntityId);

				Task.Delay(bulletDist).ContinueWith(_ =>
				{
					attacker.TurnTo(target.GetPosition());
				});
			}
			aAction.Creature.Stun = aAction.Stun;
			cap.Handle();

			attacker.Region.Broadcast(shootEffect, attacker);

			// Item Update excluding Way Of The Gun
			if (!attacker.Conditions.Has(ConditionsD.WayOfTheGun))
			{
				bulletCount -= (short)(targets.Count * 4);
				attacker.RightHand.MetaData1.SetShort(BulletCountTag, bulletCount);
				Send.ItemUpdate(attacker, attacker.RightHand);
			}

			Task.Delay(totalDelay).ContinueWith(__ =>
			{
				Send.UseMotion(attacker, 131, 4, false, false); // Bullet Storm ending motion

				/* Unlocks ---------------------
				ChangeStance|UseItem|Attack
				----------------------------- */
				attacker.Unlock(Locks.ChanceStance | Locks.UseItem | Locks.Attack);
			});

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
			Send.SkillComplete(creature, skill.Info.Id);
			creature.Skills.ActiveSkill = null;

			// Remove Leftover Locks
			creature.Unlock(Locks.Walk | Locks.Run | Locks.PickUpAndDrop | Locks.TalkToNpc);
		}

		/// <summary>
		/// Cancels the skill
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		public void Cancel(Creature creature, Skill skill)
		{
			Send.MotionCancel2(creature, 0);

			// Remove Leftover Locks
			creature.Unlock(Locks.Walk | Locks.Run | Locks.PickUpAndDrop | Locks.TalkToNpc | Locks.ChanceStance);
		}

		/// <summary>
		/// Training, called when someone attacks something.
		/// </summary>
		/// <param name="action"></param>
		public void OnCreatureAttackedByPlayer(TargetAction action)
		{
			// Guns use Combat Mastery as TargetAction skill, so check for AttackerAction skill
			if (action.AttackerSkillId != SkillId.BulletStorm)
				return;

			// Get skill
			var attackerSkill = action.Attacker.Skills.Get(SkillId.BulletStorm);
			if (attackerSkill == null) return; // Should be impossible.

			// Get targets
			var targets = action.Pack.GetTargets();

			// Learning by attacking
			switch (attackerSkill.Info.Rank)
			{
				case SkillRank.RF:
					attackerSkill.Train(1); // Attack an enemy
					if (action.Creature.IsDead) attackerSkill.Train(2); // Finishing Blow
					break;

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
					attackerSkill.Train(1); // Attack an enemy
					if (action.Creature.IsDead) attackerSkill.Train(2); // Finishing Blow
					if (action.Has(TargetOptions.Critical)) attackerSkill.Train(3); // Critical Hit
					break;
			}
		}

		/// <summary>
		/// Training, called when a creature attacks another creature(s)
		/// </summary>
		/// <param name="aAction"></param>
		public void OnCreatureAttacks(AttackerAction aAction)
		{
			// Guns use Combat Mastery as TargetAction skill, so check for AttackerAction skill
			if (aAction.SkillId != SkillId.BulletStorm)
				return;

			// Get skill
			var attackerSkill = aAction.Creature.Skills.Get(SkillId.BulletStorm);
			if (attackerSkill == null) return; // Should be impossible.

			// Get targets
			var targets = aAction.Pack.GetTargets();

			// Learning by attacking
			switch (attackerSkill.Info.Rank)
			{
				case SkillRank.RF:
					break;

				case SkillRank.RE:
					break;

				case SkillRank.RD:
					if (targets.Length >= 3) attackerSkill.Train(4); // 3 or more enemies
					break;

				case SkillRank.RC:
				case SkillRank.RB:
					if (targets.Length >= 4) attackerSkill.Train(4); // 4 or more enemies
					break;

				case SkillRank.RA:
					if (targets.Length >= 5) attackerSkill.Train(4); // 5 or more enemies
					break;

				case SkillRank.R9:
				case SkillRank.R8:
					if (targets.Length >= 5) attackerSkill.Train(4); // 5 or more enemies
					if (targets.Length >= 6) attackerSkill.Train(5); // 6 more more enemies
					break;

				case SkillRank.R7:
					if (targets.Length >= 5) attackerSkill.Train(4); // 5 or more enemies
					if (targets.Length >= 7) attackerSkill.Train(5); // 7 or mroe enemies
					break;

				case SkillRank.R6:
					if (targets.Length >= 6) attackerSkill.Train(4); // 6 or more enemies
					if (targets.Length >= 7) attackerSkill.Train(5); // 7 ore more enemies
					break;

				case SkillRank.R5:
				case SkillRank.R4:
					if (targets.Length >= 6) attackerSkill.Train(4); // 6 or more enemies
					if (targets.Length >= 8) attackerSkill.Train(5); // 8 or more enemies
					break;

				case SkillRank.R3:
				case SkillRank.R2:
					if (targets.Length >= 6) attackerSkill.Train(4); // 6 or more enemies
					if (targets.Length >= 9) attackerSkill.Train(5); // 9 or more enemies
					break;

				case SkillRank.R1:
					if (targets.Length >= 6) attackerSkill.Train(4); // 6 or more enemies
					if (targets.Length >= 10) attackerSkill.Train(5); // 10 or more enemies
					break;
			}
		}

		private Point RotatePoint(Point point, Point pivot, double radians)
		{
			var cosTheta = Math.Cos(radians);
			var sinTheta = Math.Sin(radians);

			var x = (int)(cosTheta * (point.X - pivot.X) - sinTheta * (point.Y - pivot.Y) + pivot.X);
			var y = (int)(sinTheta * (point.X - pivot.X) + cosTheta * (point.Y - pivot.Y) + pivot.Y);

			return new Point(x, y);
		}
	}
}
