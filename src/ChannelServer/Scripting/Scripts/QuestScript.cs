﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aura.Channel.World.Quests;
using System.Collections;
using Aura.Mabi.Const;
using Aura.Shared.Util;
using Aura.Channel.World.Entities;
using Aura.Mabi;
using Aura.Channel.Network.Sending;
using System.Threading;
using Aura.Channel.Skills;
using Aura.Channel.World;

namespace Aura.Channel.Scripting.Scripts
{
	public class QuestScript : GeneralScript
	{
		public int Id { get; protected set; }

		public string Name { get; protected set; }
		public string Description { get; protected set; }
		public string AdditionalInfo { get; protected set; }

		public QuestType Type { get; protected set; }
		public PtjType PtjType { get; protected set; }
		public QuestLevel Level { get; protected set; }

		public int StartHour { get; protected set; }
		public int ReportHour { get; protected set; }
		public int DeadlineHour { get; protected set; }

		public Receive ReceiveMethod { get; protected set; }
		public bool Cancelable { get; protected set; }

		public List<QuestPrerequisite> Prerequisites { get; protected set; }
		public OrderedDictionary<string, QuestObjective> Objectives { get; protected set; }
		public Dictionary<int, QuestRewardGroup> RewardGroups { get; protected set; }

		/// <summary>
		/// Used in quest items, although seemingly not required.
		/// </summary>
		public MabiDictionary MetaData { get; protected set; }

		public QuestScript()
		{
			this.Prerequisites = new List<QuestPrerequisite>();
			this.Objectives = new OrderedDictionary<string, QuestObjective>();
			this.RewardGroups = new Dictionary<int, QuestRewardGroup>();

			this.MetaData = new MabiDictionary();

			this.Type = QuestType.Normal;
		}

		public override bool Init()
		{
			this.Load();

			if (this.Id == 0 || ChannelServer.Instance.ScriptManager.QuestScripts.ContainsKey(this.Id))
			{
				Log.Error("{1}.Init: Invalid id or already in use ({0}).", this.Id, this.GetType().Name);
				return false;
			}

			if (this.Objectives.Count == 0)
			{
				Log.Error("{1}.Init: Quest '{0}' doesn't have any objectives.", this.Id, this.GetType().Name);
				return false;
			}

			if (this.ReceiveMethod == Receive.Automatically)
				ChannelServer.Instance.Events.PlayerLoggedIn += this.OnPlayerLoggedIn;

			this.MetaData.SetString("QSTTIP", "N_{0}|D_{1}|A_|R_{2}|T_0", this.Name, this.Description, string.Join(", ", this.GetDefaultRewardGroup().Rewards));

			ChannelServer.Instance.ScriptManager.QuestScripts.Add(this.Id, this);

			return true;
		}

		public override void Dispose()
		{
			base.Dispose();

			ChannelServer.Instance.Events.PlayerLoggedIn -= this.OnPlayerLoggedIn;
			ChannelServer.Instance.Events.CreatureKilledByPlayer -= this.OnCreatureKilledByPlayer;
			ChannelServer.Instance.Events.PlayerReceivesItem -= this.OnPlayerReceivesOrRemovesItem;
			ChannelServer.Instance.Events.PlayerRemovesItem -= this.OnPlayerReceivesOrRemovesItem;
			ChannelServer.Instance.Events.PlayerCompletesQuest -= this.OnPlayerCompletesQuest;
			ChannelServer.Instance.Events.SkillRankChanged -= this.OnSkillRankChanged;
			ChannelServer.Instance.Events.CreatureLevelUp -= this.OnCreatureLevelUp;
			ChannelServer.Instance.Events.CreatureGotKeyword -= this.CreatureGotKeyword;
			ChannelServer.Instance.Events.PlayerEquipsItem -= this.OnPlayerEquipsItem;
			ChannelServer.Instance.Events.CreatureGathered -= this.OnCreatureGathered;
		}

		// Setup
		// ------------------------------------------------------------------

		/// <summary>
		/// Sets id of quest.
		/// </summary>
		/// <param name="id"></param>
		protected void SetId(int id)
		{
			this.Id = id;
		}

		/// <summary>
		/// Sets name of quest.
		/// </summary>
		/// <param name="name"></param>
		protected void SetName(string name)
		{
			this.Name = name;
		}

		/// <summary>
		/// Sets description of quest.
		/// </summary>
		/// <param name="description"></param>
		protected void SetDescription(string description)
		{
			this.Description = description;
		}

		/// <summary>
		/// Sets additional info of quest.
		/// </summary>
		/// <param name="info"></param>
		protected void SetAdditionalInfo(string info)
		{
			this.AdditionalInfo = info;
		}

		/// <summary>
		/// Sets type of quest.
		/// </summary>
		/// <param name="type"></param>
		protected void SetType(QuestType type)
		{
			this.Type = type;
		}

		/// <summary>
		/// Sets PTJ hours.
		/// </summary>
		/// <param name="start"></param>
		/// <param name="report"></param>
		/// <param name="report"></param>
		protected void SetHours(int start, int report, int deadline)
		{
			this.StartHour = start;
			this.ReportHour = report;
			this.DeadlineHour = deadline;
		}

		/// <summary>
		/// Sets type for PTJs.
		/// </summary>
		/// <param name="type"></param>
		protected void SetPtjType(PtjType type)
		{
			this.PtjType = type;
		}

		/// <summary>
		/// Sets quest's level (required for PTJ).
		/// </summary>
		/// <param name="level"></param>
		protected void SetLevel(QuestLevel level)
		{
			this.Level = level;
		}

		/// <summary>
		/// Sets the way you receive the quest.
		/// </summary>
		/// <param name="method"></param>
		protected void SetReceive(Receive method)
		{
			this.ReceiveMethod = method;
		}

		/// <summary>
		/// Adds prerequisite that has to be met before auto receiving the quest.
		/// </summary>
		/// <param name="prerequisite"></param>
		protected void AddPrerequisite(QuestPrerequisite prerequisite)
		{
			this.Prerequisites.Add(prerequisite);

			if (prerequisite is QuestPrerequisiteQuestCompleted)
			{
				ChannelServer.Instance.Events.PlayerCompletesQuest -= this.OnPlayerCompletesQuest;
				ChannelServer.Instance.Events.PlayerCompletesQuest += this.OnPlayerCompletesQuest;
			}

			if (prerequisite is QuestPrerequisiteReachedLevel || prerequisite is QuestPrerequisiteReachedTotalLevel)
			{
				ChannelServer.Instance.Events.CreatureLevelUp -= this.OnCreatureLevelUp;
				ChannelServer.Instance.Events.CreatureLevelUp += this.OnCreatureLevelUp;
			}
		}

		/// <summary>
		/// Adds objective that has to be cleared to complete the quest.
		/// </summary>
		/// <param name="ident"></param>
		/// <param name="description"></param>
		/// <param name="regionId"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="objective"></param>
		protected void AddObjective(string ident, string description, int regionId, int x, int y, QuestObjective objective)
		{
			if (this.Objectives.ContainsKey(ident))
			{
				Log.Error("{0}: Objectives must have an unique identifier.", this.GetType().Name);
				return;
			}

			objective.Ident = ident;
			objective.Description = description;
			objective.RegionId = regionId;
			objective.X = x;
			objective.Y = y;

			if (objective.Type == ObjectiveType.Kill)
			{
				ChannelServer.Instance.Events.CreatureKilledByPlayer -= this.OnCreatureKilledByPlayer;
				ChannelServer.Instance.Events.CreatureKilledByPlayer += this.OnCreatureKilledByPlayer;
			}

			if (objective.Type == ObjectiveType.Collect)
			{
				ChannelServer.Instance.Events.PlayerReceivesItem -= this.OnPlayerReceivesOrRemovesItem;
				ChannelServer.Instance.Events.PlayerReceivesItem += this.OnPlayerReceivesOrRemovesItem;
				ChannelServer.Instance.Events.PlayerRemovesItem -= this.OnPlayerReceivesOrRemovesItem;
				ChannelServer.Instance.Events.PlayerRemovesItem += this.OnPlayerReceivesOrRemovesItem;
			}

			if (objective.Type == ObjectiveType.ReachRank)
			{
				ChannelServer.Instance.Events.SkillRankChanged -= this.OnSkillRankChanged;
				ChannelServer.Instance.Events.SkillRankChanged += this.OnSkillRankChanged;
			}

			if (objective.Type == ObjectiveType.ReachLevel)
			{
				ChannelServer.Instance.Events.CreatureLevelUp -= this.OnCreatureLevelUp;
				ChannelServer.Instance.Events.CreatureLevelUp += this.OnCreatureLevelUp;
			}

			if (objective.Type == ObjectiveType.GetKeyword)
			{
				ChannelServer.Instance.Events.CreatureGotKeyword -= this.CreatureGotKeyword;
				ChannelServer.Instance.Events.CreatureGotKeyword += this.CreatureGotKeyword;
			}

			if (objective.Type == ObjectiveType.Equip)
			{
				ChannelServer.Instance.Events.PlayerEquipsItem -= this.OnPlayerEquipsItem;
				ChannelServer.Instance.Events.PlayerEquipsItem += this.OnPlayerEquipsItem;
			}

			if (objective.Type == ObjectiveType.Gather)
			{
				ChannelServer.Instance.Events.CreatureGathered -= this.OnCreatureGathered;
				ChannelServer.Instance.Events.CreatureGathered += this.OnCreatureGathered;
			}

			this.Objectives.Add(ident, objective);
		}

		protected void AddReward(QuestReward reward)
		{
			this.AddReward(0, RewardGroupType.Item, QuestResult.Perfect, reward);
		}

		protected void AddReward(int groupId, RewardGroupType type, QuestResult result, QuestReward reward)
		{
			if (!this.RewardGroups.ContainsKey(groupId))
				this.RewardGroups[groupId] = new QuestRewardGroup(groupId, type);

			reward.Result = result;

			this.RewardGroups[groupId].Add(reward);
		}

		public QuestRewardGroup GetDefaultRewardGroup()
		{
			QuestRewardGroup result;
			if (!this.RewardGroups.TryGetValue(0, out result))
				if (!this.RewardGroups.TryGetValue(1, out result))
					throw new Exception("QuestScript.GetDefaultRewardGroup: No default group found.");

			return result;
		}

		public ICollection<QuestReward> GetRewards(int rewardGroup, QuestResult result)
		{
			var rewards = new List<QuestReward>();

			QuestRewardGroup group;
			this.RewardGroups.TryGetValue(rewardGroup, out group);
			if (group != null && result != QuestResult.None)
				rewards.AddRange(group.Rewards.Where(a => a.Result == result));

			return rewards;
		}

		// Prerequisite Factory
		// ------------------------------------------------------------------

		protected QuestPrerequisite Completed(int questId) { return new QuestPrerequisiteQuestCompleted(questId); }
		protected QuestPrerequisite ReachedLevel(int level) { return new QuestPrerequisiteReachedLevel(level); }
		protected QuestPrerequisite ReachedTotalLevel(int level) { return new QuestPrerequisiteReachedTotalLevel(level); }
		protected QuestPrerequisite NotSkill(SkillId skillId, SkillRank rank = SkillRank.Novice) { return new QuestPrerequisiteNotSkill(skillId, rank); }
		protected QuestPrerequisite And(params QuestPrerequisite[] prerequisites) { return new QuestPrerequisiteAnd(prerequisites); }
		protected QuestPrerequisite Or(params QuestPrerequisite[] prerequisites) { return new QuestPrerequisiteOr(prerequisites); }

		// Objective Factory
		// ------------------------------------------------------------------

		protected QuestObjective Kill(int amount, string raceType) { return new QuestObjectiveKill(amount, raceType); }
		protected QuestObjective Collect(int itemId, int amount) { return new QuestObjectiveCollect(itemId, amount); }
		protected QuestObjective Talk(string npcName) { return new QuestObjectiveTalk(npcName); }
		protected QuestObjective ReachRank(SkillId skillId, SkillRank rank) { return new QuestObjectiveReachRank(skillId, rank); }
		protected QuestObjective ReachLevel(int level) { return new QuestObjectiveReachLevel(level); }
		protected QuestObjective GetKeyword(string keyword) { return new QuestObjectiveGetKeyword(keyword); }
		protected QuestObjective Equip(string tag) { return new QuestObjectiveEquip(tag); }
		protected QuestObjective Gather(int itemId, int amount) { return new QuestObjectiveGather(itemId, amount); }

		// Reward Factory
		// ------------------------------------------------------------------

		protected QuestReward Item(int itemId, int amount = 1) { return new QuestRewardItem(itemId, amount); }
		protected QuestReward Skill(SkillId skillId, SkillRank rank) { return new QuestRewardSkill(skillId, rank); }
		protected QuestReward Gold(int amount) { return new QuestRewardGold(amount); }
		protected QuestReward Exp(int amount) { return new QuestRewardExp(amount); }
		protected QuestReward ExplExp(int amount) { return new QuestRewardExplExp(amount); }
		protected QuestReward AP(short amount) { return new QuestRewardAp(amount); }

		// Where the magic happens~
		// ------------------------------------------------------------------

		/// <summary>
		/// Checks and starts auto quests.
		/// </summary>
		/// <param name="character"></param>
		private void OnPlayerLoggedIn(Creature character)
		{
			if (this.CheckPrerequisites(character))
				character.Quests.Start(this.Id, true);
		}

		/// <summary>
		/// Starts quest if prerequisites are met.
		/// </summary>
		/// <param name="character"></param>
		/// <returns></returns>
		private bool CheckPrerequisites(Creature character)
		{
			if (this.ReceiveMethod != Receive.Automatically || character.Quests.Has(this.Id))
				return false;

			return this.Prerequisites.All(prerequisite => prerequisite.Met(character));
		}

		/// <summary>
		/// Checks and updates current obective's count.
		/// </summary>
		/// <param name="creature"></param>
		public void CheckCurrentObjective(Creature creature)
		{
			if (creature == null || !creature.IsPlayer)
				return;

			var quest = creature.Quests.Get(this.Id);
			if (quest == null) return;

			var progress = quest.CurrentObjectiveOrLast;
			if (progress == null) return;

			var objective = this.Objectives[progress.Ident];
			if (objective == null) return;

			var prevCount = progress.Count;
			switch (objective.Type)
			{
				case ObjectiveType.ReachRank:
					var reachRankObjective = (objective as QuestObjectiveReachRank);
					var skillId = reachRankObjective.Id;
					var rank = reachRankObjective.Rank;
					var skill = creature.Skills.Get(skillId);

					if (skill != null && skill.Info.Rank >= rank)
						quest.SetDone(progress.Ident);
					else
						quest.SetUndone(progress.Ident);

					break;

				case ObjectiveType.ReachLevel:
					var reachLevelObjective = (objective as QuestObjectiveReachLevel);

					if (creature.Level >= reachLevelObjective.Amount)
						quest.SetDone(progress.Ident);

					break;

				case ObjectiveType.Collect:
					var itemId = (objective as QuestObjectiveCollect).ItemId;
					var count = creature.Inventory.Count(itemId);

					if (!progress.Done && count >= objective.Amount)
						quest.SetDone(progress.Ident);
					else if (progress.Done && count < objective.Amount)
						quest.SetUndone(progress.Ident);

					// Set(Un)Done modifies the count, has to be set afterwards
					progress.Count = count;
					break;

				case ObjectiveType.GetKeyword:
					var getKeywordObjective = (objective as QuestObjectiveGetKeyword);

					if (creature.Keywords.Has((ushort)getKeywordObjective.KeywordId))
						quest.SetDone(progress.Ident);

					break;

				default:
					// Objective that can't be checked here.
					break;
			}

			if (progress.Count != prevCount)
				Send.QuestUpdate(creature, quest);
		}

		/// <summary>
		/// Updates kill objectives.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="killer"></param>
		private void OnCreatureKilledByPlayer(Creature creature, Creature killer)
		{
			if (creature == null || killer == null) return;

			var quest = killer.Quests.Get(this.Id);
			if (quest == null) return;

			var progress = quest.CurrentObjective;
			if (progress == null) return;

			var objective = this.Objectives[progress.Ident] as QuestObjectiveKill;
			if (objective == null || objective.Type != ObjectiveType.Kill || !objective.Check(creature)) return;

			if (progress.Count >= objective.Amount) return;

			progress.Count++;

			if (progress.Count >= objective.Amount)
				quest.SetDone(progress.Ident);

			Send.QuestUpdate(killer, quest);
		}

		/// <summary>
		/// Updates collect objectives.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="itemId"></param>
		/// <param name="amount"></param>
		private void OnPlayerReceivesOrRemovesItem(Creature creature, int itemId, int amount)
		{
			this.CheckCurrentObjective(creature);
		}

		/// <summary>
		/// Updates reach rank objectives.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		private void OnSkillRankChanged(Creature creature, Skill skill)
		{
			this.CheckCurrentObjective(creature);
		}

		/// <summary>
		/// Checks prerequisites.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="questId"></param>
		private void OnPlayerCompletesQuest(Creature creature, int questId)
		{
			if (this.CheckPrerequisites(creature))
				creature.Quests.Start(this.Id, true);
		}

		/// <summary>
		/// Checks prerequisites.
		/// </summary>
		/// <param name="creature"></param>
		private void OnCreatureLevelUp(Creature creature)
		{
			if (!creature.Quests.Has(this.Id) && this.CheckPrerequisites(creature))
				creature.Quests.Start(this.Id, true);

			this.CheckCurrentObjective(creature);
		}

		/// <summary>
		/// Checks and updates current objective.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="keywordId"></param>
		private void CreatureGotKeyword(Creature creature, int keywordId)
		{
			this.CheckCurrentObjective(creature);
		}

		/// <summary>
		/// Updates equip objectives.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="item"></param>
		private void OnPlayerEquipsItem(Creature creature, Item item)
		{
			if (creature == null || !creature.IsPlayer || item == null || !item.Info.Pocket.IsEquip())
				return;

			var quest = creature.Quests.Get(this.Id);
			if (quest == null) return;

			var progress = quest.CurrentObjectiveOrLast;
			if (progress == null) return;

			var objective = this.Objectives[progress.Ident];
			if (objective == null || objective.Type != ObjectiveType.Equip) return;

			var equipObjective = (objective as QuestObjectiveEquip);
			if (!progress.Done && item.HasTag(equipObjective.Tag))
			{
				quest.SetDone(progress.Ident);
				Send.QuestUpdate(creature, quest);
			}
		}

		/// <summary>
		/// Updates gathering objectives.
		/// </summary>
		/// <param name="args"></param>
		private void OnCreatureGathered(CollectEventArgs args)
		{
			var quest = args.Creature.Quests.Get(this.Id);
			if (quest == null) return;

			var progress = quest.CurrentObjectiveOrLast;
			if (progress == null) return;

			var objective = this.Objectives[progress.Ident];
			if (objective == null || objective.Type != ObjectiveType.Gather) return;

			var gatherObjective = (objective as QuestObjectiveGather);
			if (!progress.Done && args.Success && args.ItemId == gatherObjective.ItemId)
			{
				progress.Count++;
				if (progress.Count == gatherObjective.Amount)
					quest.SetDone(progress.Ident);

				Send.QuestUpdate(args.Creature, quest);
			}
		}
	}

	public enum Receive
	{
		Manually,
		Automatically,
	}

	public enum QuestLevel
	{
		None,
		Basic,
		Int,
		Adv,
	}
}
