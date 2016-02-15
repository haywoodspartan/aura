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
	/// Shooting Rush skill handler
	/// </summary>
	/// Var1: Bullet Use
	/// Var2: Damage (Per Bullet)
	/// Var5: Attack Spread
	/// Var6: Attack Distance
	[Skill(SkillId.ShootingRush)]
	public class ShootingRush : ISkillHandler, IPreparable, IReadyable, IUseable, ICompletable, ICancelable, IInitiableSkillHandler
	{
		/// <summary>
		/// Bullet Count Tag for Gun
		/// </summary>
		private const string BulletCountTag = "GVBC";

		/// <summary>
		/// Attacker's stun upon using the skill
		/// </summary>
		private const int AttackerStun = 0;

		/// <summary>
		/// Target's stun after getting hit
		/// </summary>
		private const int TargetStun = 3500;

		/// <summary>
		/// Distance target gets knocked back
		/// </summary>
		private const int KnockbackDistance = 300; // Unofficial

		/// <summary>
		/// Target's stability reduction
		/// </summary>
		private const int StabilityReduction = 10;

		// <summary>
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
			Send.UseMotion(creature, 131, 5, true, false);
			Send.Effect(creature, Effect.SkillInit, "flashing");

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
			Send.Effect(creature, Effect.SkillInit, "counter_attack", (byte)0);
			skill.Stacks = 1;
			Send.SkillReady(creature, skill.Info.Id);

			return true;
		}

		/// <summary>
		/// Uses Shooting Rush
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public void Use(Creature attacker, Skill skill, Packet packet)
		{
			var targetAreaId = packet.GetLong();
			var targetAreaLoc = new Location(targetAreaId);
			var targetAreaPos = new Position(targetAreaLoc.X, targetAreaLoc.Y);

			var attackerPos = attacker.GetPosition();

			// Distance (Length) & Radius (Width)
			var distance = skill.RankData.Var6;
			var radius = skill.RankData.Var5;

			attacker.StopMove();

			var relativeDistance = attackerPos.GetDistance(targetAreaPos);

			Position newAttackerPos = new Position(0, 0);

			// New Attacker position based on skill distance.
			if (relativeDistance < distance) // Add distance if distance to targetArea is too short.
			{
				var extraDistance = distance - relativeDistance;
				newAttackerPos = attackerPos.GetRelative(targetAreaPos, (int)extraDistance);
			}
			else if (relativeDistance > distance) // Go between if distance to targetArea is too long.
			{
				var extraDistance = relativeDistance - distance;
				newAttackerPos = attackerPos.GetRelative(targetAreaPos, ((int)extraDistance * -1));
			}

			attacker.TurnTo(newAttackerPos);

			// Effects & Etc
			Send.Effect(attacker, 335, (byte)1, (byte)1, (short)131, (short)6, 1599, (byte)6, (short)66, (short)233, (short)500, (short)700, (short)966, (short)1166); // ?
			Send.MotionCancel2(attacker, 0);
			Send.UseMotion(attacker, 131, 6, false, false); // Shooting Rush Motion

			var unkPacket2 = new Packet(0x6D64, attacker.EntityId); // ?
			unkPacket2.PutInt(1599).PutInt(131).PutInt(7).PutByte(0).PutShort(0);
			attacker.Region.Broadcast(unkPacket2, attacker);

			Send.Effect(attacker, 329, (byte)1, (byte)0); // ?
			attacker.Conditions.Activate(ConditionsD.Steadfast);
			Send.Effect(attacker, 340, (byte)1, (float)newAttackerPos.X, (float)newAttackerPos.Y, (float)distance, 1599); // ??

			// Set creature position to new position.
			attacker.SetPosition(newAttackerPos.X, newAttackerPos.Y);

			// Center Points Calculation
			var attackerPoint = new Point(attackerPos.X, attackerPos.Y);
			var newAttackerPoint = new Point(newAttackerPos.X, newAttackerPos.Y);

			var pointDist = Math.Sqrt((distance * distance) + (radius * radius)); // Pythagorean Theorem - Distance between point and opposite side's center.
			var rotationAngle = Math.Asin(radius / pointDist);

			// Note: My previous error was that I used pointDist for the GetRelative distance,
			// which was wrong since it adds that distance to the current distance.
			// The only extra distance I needed was that of the pointDist - distance.

			// Calculate Points 1 & 2
			var posTemp1 = attackerPos.GetRelative(newAttackerPos, (int)(pointDist - distance));
			var pointTemp1 = new Point(posTemp1.X, posTemp1.Y);
			var p1 = this.RotatePoint(pointTemp1, attackerPoint, rotationAngle); // Rotate Positive - moves point to position where distance from newAttackerPos is range and Distance from attackerPos is pointDist.
			var p2 = this.RotatePoint(pointTemp1, attackerPoint, (rotationAngle * -1)); // Rotate Negative - moves point to opposite side of p1

			// Calculate Points 3 & 4
			var posTemp2 = newAttackerPos.GetRelative(attackerPos, (int)(pointDist - distance));
			var pointTemp2 = new Point(posTemp2.X, posTemp2.Y);
			var p3 = this.RotatePoint(pointTemp2, newAttackerPoint, rotationAngle); // Rotate Positive
			var p4 = this.RotatePoint(pointTemp2, newAttackerPoint, (rotationAngle * -1)); // Rotate Negative

			// Prepare attacker action
			var cap = new CombatActionPack(attacker, skill.Info.Id);

			var aAction = new AttackerAction(CombatActionType.SpecialHit, attacker, targetAreaId);
			aAction.Set(AttackerOptions.UseEffect | AttackerOptions.KnockBackHit1);
			cap.Add(aAction);

			aAction.Stun = AttackerStun;

			// Prepare Multi-Enemy effect
			var shootEffect = new Packet(Op.Effect, attacker.EntityId);
			shootEffect.PutInt(Effect.BulletTrail).PutShort((short)skill.Info.Id).PutInt(0);
			var bulletDist = 0;

			// Get targets in descending order of distance for the effect packet
			var targets = attacker.Region.GetCreaturesInPolygon(p1, p2, p3, p4).Where(x => attacker.CanTarget(x)).OrderByDescending(x => x.GetPosition().GetDistance(attackerPos)).ToList();

			// Add Target count to effect packet
			shootEffect.PutShort((short)targets.Count);

			var rnd = RandomProvider.Get();

			// Check crit
			var crit = false;
			var critSkill = attacker.Skills.Get(SkillId.CriticalHit);
			if (critSkill != null && critSkill.Info.Rank > SkillRank.Novice)
			{
				var dgm = attacker.Skills.Get(SkillId.DualGunMastery);
				var extraCritChance = (dgm == null ? 0 : dgm.RankData.Var6);
				var critChance = Math2.Clamp(0, 30, attacker.GetTotalCritChance(0) + extraCritChance);
				if (rnd.NextDouble() * 100 < critChance)
					crit = true;
			}

			// Prepare target actions
			foreach (var target in targets)
			{
				var tAction = new TargetAction(CombatActionType.SkillActiveHit, target, attacker, SkillId.None);
				tAction.Set(TargetOptions.Result | TargetOptions.MultiHit);
				tAction.AttackerSkillId = skill.Info.Id;
				tAction.MultiHitDamageCount = 4;
				tAction.MultiHitDamageShowTime = 300;
				tAction.MultiHitUnk1 = 0;
				tAction.MultiHitUnk2 = 404166685;
                cap.Add(tAction);

				// Damage
				var damage = (attacker.GetRndDualGunDamage() * (skill.RankData.Var2 / 100f)) * tAction.MultiHitDamageCount;

				// Master Title
				if (attacker.Titles.SelectedTitle == 10917)
					damage += (damage * (12 / 100f)); // +12% damage

				// Critical Hit
				if (crit)
				{
					var bonus = critSkill.RankData.Var1 / 100f;
					damage = damage + (damage * bonus);

					tAction.Set(TargetOptions.Critical);
				}

				// Defense and Prot
				SkillHelper.HandleDefenseProtection(target, ref damage);

				// Defense
				Defense.Handle(aAction, tAction, ref damage);

				// Mana Shield
				ManaShield.Handle(target, ref damage, tAction);

				// Natural Shield
				NaturalShield.Handle(attacker, target, ref damage, tAction);

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

					// Always knock down
					if (target.Is(RaceStands.KnockDownable))
					{
						tAction.Set(TargetOptions.KnockDown);
						attacker.Shove(target, KnockbackDistance);
					}
					tAction.Creature.Stun = tAction.Stun;
				}

				bulletDist = target.GetPosition().GetDistance(attackerPos);
				tAction.Delay = bulletDist;
				shootEffect.PutInt(bulletDist).PutLong(target.EntityId);
			}
			aAction.Creature.Stun = aAction.Stun;
			cap.Handle();

			Send.SkillUse(attacker, skill.Info.Id, targetAreaId, 0, 1);
			skill.Stacks = 0;
			attacker.Region.Broadcast(shootEffect, attacker);

			// Item Update excluding Way Of The Gun
			if (!attacker.Conditions.Has(ConditionsD.WayOfTheGun))
			{
				var bulletCount = attacker.RightHand.MetaData1.GetShort(BulletCountTag);
				bulletCount -= (short)skill.RankData.Var1; // 8 Bullets
				attacker.RightHand.MetaData1.SetShort(BulletCountTag, bulletCount);
				Send.ItemUpdate(attacker, attacker.RightHand);
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
			Send.Effect(creature, 329, (byte)0);
			creature.Conditions.Deactivate(ConditionsD.Steadfast);
			Send.Effect(creature, 340, (byte)0);
			Send.SkillComplete(creature, skill.Info.Id);
		}

		/// <summary>
		/// Cancels the skill
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		public void Cancel(Creature creature, Skill skill)
		{
			Send.MotionCancel2(creature, 0);
		}

		/// <summary>
		/// Training, called when someone attacks something.
		/// </summary>
		/// <param name="action"></param>
		public void OnCreatureAttackedByPlayer(TargetAction action)
		{
			// Guns use Combat Mastery as TargetAction skill, so check for AttackerAction skill
			if (action.AttackerSkillId != SkillId.ShootingRush)
				return;

			// Get skill
			var attackerSkill = action.Attacker.Skills.Get(SkillId.ShootingRush);
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
			if (aAction.SkillId != SkillId.ShootingRush)
				return;

			// Get skill
			var attackerSkill = aAction.Creature.Skills.Get(SkillId.ShootingRush);
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
				case SkillRank.RC:
				case SkillRank.RB:
					if (targets.Length >= 3) attackerSkill.Train(4); // 3 or more enemies
					break;

				case SkillRank.RA:
					if (targets.Length >= 4) attackerSkill.Train(4); // 4 or more enemies
					break;

				case SkillRank.R9:
				case SkillRank.R8:
					if (targets.Length >= 4) attackerSkill.Train(4); // 4 or more enemies
					if (targets.Length >= 5) attackerSkill.Train(5); // 5 or more enemies
					break;

				case SkillRank.R7:
				case SkillRank.R6:
				case SkillRank.R5:
					if (targets.Length >= 5) attackerSkill.Train(4); // 5 or more enemies
					if (targets.Length >= 6) attackerSkill.Train(5); // 6 or more enemies
					break;

				case SkillRank.R4:
					if (targets.Length >= 5) attackerSkill.Train(4); // 5 or more enemies
					if (targets.Length >= 7) attackerSkill.Train(5); // 7 or more enemies
					break;

				case SkillRank.R3:
				case SkillRank.R2:
				case SkillRank.R1:
					if (targets.Length >= 6) attackerSkill.Train(4); // 6 or more enemies
					if (targets.Length >= 7) attackerSkill.Train(5); // 7 or more enemies
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
