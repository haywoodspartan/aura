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
	public class ShootingRush : ISkillHandler, IPreparable, IReadyable, IUseable, ICompletable, ICancelable
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
			var radius = skill.RankData.Var5 / 2;

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

			var unkPacket3 = new Packet(0x7534, attacker.EntityId);
			unkPacket3.PutByte(0).PutByte(0).PutByte(0);
			attacker.Region.Broadcast(unkPacket3, attacker);

			// Effects & Etc
			Send.Effect(attacker, 335, (byte)1, (byte)1, (short)131, (short)6, 1599, (byte)6, (short)66, (short)233, (short)500, (short)700, (short)966, (short)1166);
			Send.MotionCancel2(attacker, 0);
			Send.UseMotion(attacker, 131, 6, false, false);

			var unkPacket = new Packet(0x6D64, attacker.EntityId); // ?
			unkPacket.PutInt(1599).PutInt(131).PutInt(7).PutByte(0).PutShort(0);
			attacker.Region.Broadcast(unkPacket, attacker);

			Send.Effect(attacker, 329, (byte)1, (byte)0);
			attacker.Conditions.Activate(ConditionsD.Steadfast);
			Send.Effect(attacker, 340, (byte)1, (float)newAttackerPos.X, (float)newAttackerPos.Y, (float)distance, 1599);

			// Set creature position to new position.
			attacker.SetPosition(newAttackerPos.X, newAttackerPos.Y);

			// Center Points Calculation
			var attackerPoint = new Point(attackerPos.X, attackerPos.Y);
			var newAttackerPoint = new Point(newAttackerPos.X, newAttackerPos.Y);

			// Calculate Points 1 & 2
			var pointDist = Math.Sqrt((distance * distance) + (radius * radius)); // Pythagorean Theorem - Distance between point and opposite side's center.
			var p1PosTemp = attackerPos.GetRelative(newAttackerPos, (int)pointDist);
			var p1PointTemp = new Point(p1PosTemp.X, p1PosTemp.Y);
			var rotationAngle = Math.Asin(radius / pointDist);
			var p1 = this.RotatePoint(p1PointTemp, attackerPoint, rotationAngle); // Rotate Positive - moves point to position where distance from newAttackerPos is range and Distance from attackerPos is pointDist.
			var p2 = this.RotatePoint(p1PointTemp, attackerPoint, (rotationAngle * -1)); // Rotate Negative - moves point to opposite side of p1

			// Calculate Points 3 & 4
			var p2PosTemp = newAttackerPos.GetRelative(attackerPos, (int)pointDist);
			var p2PointTemp = new Point(p2PosTemp.X, p2PosTemp.Y);
			var p3 = this.RotatePoint(p2PointTemp, newAttackerPoint, rotationAngle); // Rotate Positive
			var p4 = this.RotatePoint(p2PointTemp, newAttackerPoint, (rotationAngle * -1)); // Rotate Negative

			// Prepare Combat Actions
			var cap = new CombatActionPack(attacker, skill.Info.Id);

			var aAction = new AttackerAction(CombatActionType.SpecialHit, attacker, skill.Info.Id, targetAreaId);
			aAction.Set(AttackerOptions.UseEffect | AttackerOptions.KnockBackHit1);
			cap.Add(aAction);

			aAction.Stun = AttackerStun;

			// Prepare effect to send
			var unkPacket4 = new Packet(Op.Effect, attacker.EntityId);
			unkPacket4.PutInt(339).PutShort((short)skill.Info.Id).PutInt(0).PutShort(2);

			var bulletTime = 3850; // Unofficial

			var targets = attacker.Region.GetCreaturesInPolygon(p1, p2, p3, p4);
			foreach (var target in targets.Where(cr => !cr.IsDead && attacker.CanTarget(cr)))
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
				var damage = (attacker.GetRndDualGunDamage() * (skill.RankData.Var2 / 100f)) * 4;

				// Master Title
				if (attacker.Titles.SelectedTitle == 10917)
					damage += (damage * (12 / 100f)); // +12% damage

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

				Send.Effect(target, 298, (byte)0);
				Send.Effect(target, 298, (byte)0);

				bulletTime /= 7;

				unkPacket4.PutInt(bulletTime).PutLong(target.EntityId);
			}
			aAction.Creature.Stun = aAction.Stun;
			cap.Handle();

			var unkPacket2 = new Packet(0x7534, attacker.EntityId);
			unkPacket2.PutByte(0).PutByte(0).PutByte(0);
			attacker.Region.Broadcast(unkPacket2, attacker);

			Send.SkillUse(attacker, skill.Info.Id, targetAreaId, 0, 1);
			skill.Stacks = 0;
			attacker.Region.Broadcast(unkPacket4, attacker);

			// Item Update
			var bulletCount = attacker.RightHand.MetaData1.GetShort(BulletCountTag);
			bulletCount -= (short)skill.RankData.Var1; // 8 Bullets
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
