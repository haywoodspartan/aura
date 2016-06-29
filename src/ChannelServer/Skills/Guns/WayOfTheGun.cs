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
using System.Threading;

namespace Aura.Channel.Skills.Guns
{
	/// <summary>
	/// Way Of The Gun skill handler
	/// </summary>
	/// Var1: Duration
	/// Var2: Attack Speed
	[Skill(SkillId.WayOfTheGun)]
	public class WayOfTheGun : ISkillHandler, IPreparable, ICompletable, IInitiableSkillHandler
	{
		/// <summary>
		/// Bullet Count Tag for Gun
		/// </summary>
		private const string BulletCountTag = "GVBC";

		/// <summary>
		/// Bullet Min damage tag
		/// </summary>
		private const string GBAMINTag = "GBAMIN";

		/// <summary>
		/// Bullet Max damage tag
		/// </summary>
		private const string GBAMAXTag = "GBAMAX";

		// <summary>
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

			// Clear creature temp variables
			// Just for safety
			creature.Temp.WOTGKillCount = 0;

			creature.StopMove();

			// Set Gun Ammo to 0
			creature.RightHand.MetaData1.SetShort(BulletCountTag, 0);
			Send.ItemUpdate(creature, creature.RightHand);

			// Activate WOTG Condition
			creature.Conditions.Activate(ConditionsD.WayOfTheGun);

			// Unk Packet
			var p = new Packet(0x7534, creature.EntityId);
			p.PutByte(0).PutByte(0).PutByte(0);
			creature.Region.Broadcast(p, creature);

			// Skill Use
			Send.SkillUse(creature, skill.Info.Id);
			skill.State = SkillState.Used;

			var duration = (int)skill.RankData.Var1;

			// Master Title - +5 seconds
			if (creature.Titles.SelectedTitle == skill.Data.MasterTitle)
				duration += 5;

			// Deactivate condition after skill duration
			Task.Delay(duration).ContinueWith(_ =>
			{
				if (creature.Conditions.Has(ConditionsD.WayOfTheGun))
				{
					creature.Conditions.Deactivate(ConditionsD.WayOfTheGun);
					Send.MotionCancel2(creature, 0);
				}
			});

			// Train
			skill.Train(1); // Use the skill

			return true;
		}

		/// <summary>
		/// Sends SkillComplete
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="packet"></param>
		public void Complete(Creature creature, Skill skill, Packet packet)
		{
			Send.SkillComplete(creature, skill.Info.Id);
		}

		/// <summary>
		/// Training, called when someone attacks something.
		/// </summary>
		/// <param name="action"></param>
		public void OnCreatureAttackedByPlayer(TargetAction action)
		{
			// Skill takes all attacks used by guns to train.
			// Specifying skill shouldn't be an issue since WOTG will deactivate if weapons are switched / removed.
			// Skills like counterattack and bolts also work... i think.
			if (!action.Attacker.Conditions.Has(ConditionsD.WayOfTheGun))
				return;

			// Get skill
			var attackerSkill = action.Attacker.Skills.Get(SkillId.WayOfTheGun);
			if (attackerSkill == null) return; // Should be impossible.

			// Kill counter
			if (action.Creature.IsDead)
				action.Attacker.Temp.WOTGKillCount += 1;

			// Learning by attacking
			switch (attackerSkill.Info.Rank)
			{
				case SkillRank.RF:
				case SkillRank.RE:
				case SkillRank.RD:
					attackerSkill.Train(2); // Attack an enemy
					break;

				case SkillRank.RC:
				case SkillRank.RB:
				case SkillRank.RA:
					attackerSkill.Train(2); // Attack an enemy
					if (action.Creature.IsDead) attackerSkill.Train(3); // Defeat an enemy
					break;

				case SkillRank.R9:
				case SkillRank.R8:
				case SkillRank.R7:
					attackerSkill.Train(2); // Attack an enemy
					if (action.Creature.IsDead) attackerSkill.Train(3); // Defeat an enemy
					if (action.Attacker.Temp.WOTGKillCount == 2)
					{
						attackerSkill.Train(4); // Defeat 2 or more enemies
						action.Attacker.Temp.WOTGKillCount = 0;
					}
					break;

				case SkillRank.R6:
				case SkillRank.R5:
				case SkillRank.R4:
					attackerSkill.Train(2); // Attack an enemy
					if (action.Creature.IsDead) attackerSkill.Train(3); // Defeat an enemy
					if (action.Attacker.Temp.WOTGKillCount == 3)
					{
						attackerSkill.Train(4); // Defeat 3 or more enemies
						action.Attacker.Temp.WOTGKillCount = 0;
					}
					break;

				case SkillRank.R3:
				case SkillRank.R2:
				case SkillRank.R1:
					attackerSkill.Train(2); // Attack an enemy
					if (action.Creature.IsDead) attackerSkill.Train(3); // Defeat an enemy
					if (action.Attacker.Temp.WOTGKillCount == 4)
					{
						attackerSkill.Train(4); // Defeat 4 or more enemies
						action.Attacker.Temp.WOTGKillCount = 0;
					}
					break;
			}
		}
	}
}
