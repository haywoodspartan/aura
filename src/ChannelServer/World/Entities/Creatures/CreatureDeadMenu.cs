﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System.Text;
using Aura.Mabi.Const;
using Aura.Channel.World.Dungeons;
using Aura.Channel.Network.Sending;

namespace Aura.Channel.World.Entities.Creatures
{
	public class CreatureDeadMenu
	{
		public Creature Creature { get; private set; }

		public ReviveOptions Options { get; set; }

		public CreatureDeadMenu(Creature creature)
		{
			this.Creature = creature;
		}

		public void Add(ReviveOptions option)
		{
			this.Options |= option;
		}

		public bool Has(ReviveOptions option)
		{
			return ((this.Options & option) != 0);
		}

		public void Clear()
		{
			this.Options = ReviveOptions.None;
		}

		public override string ToString()
		{
			var sb = new StringBuilder();

			if (this.Has(ReviveOptions.ArenaLobby))
				sb.Append("arena_lobby;");
			if (this.Has(ReviveOptions.ArenaSide))
				sb.Append("arena_side;");
			if (this.Has(ReviveOptions.ArenaWaitingRoom))
				sb.Append("arena_waiting;");
			if (this.Has(ReviveOptions.BarriLobby))
				sb.Append("barri_lobby;");
			if (this.Has(ReviveOptions.NaoStone))
				sb.Append("naocoupon;");
			if (this.Has(ReviveOptions.DungeonEntrance))
				sb.Append("dungeon_lobby;");
			if (this.Has(ReviveOptions.Here))
				sb.Append("here;");
			if (this.Has(ReviveOptions.HereNoPenalty))
				sb.Append("trnsfrm_pvp_here;");
			if (this.Has(ReviveOptions.HerePvP))
				sb.Append("showdown_pvp_here;");
			if (this.Has(ReviveOptions.InCamp))
				sb.Append("camp;");
			if (this.Has(ReviveOptions.StatueOfGoddess))
				sb.Append("dungeon_statue;");
			if (this.Has(ReviveOptions.TirChonaill))
				sb.Append("tirchonaill;");
			if (this.Has(ReviveOptions.Town))
				sb.Append("town;");
			if (this.Has(ReviveOptions.WaitForRescue))
				sb.Append("stay;");

			return sb.ToString().Trim(';');
		}

		/// <summary>
		/// Updates menu, based on where its creature is and updates
		/// nearby clients.
		/// </summary>
		public void Update()
		{
			var before = this.Options;

			this.Clear();

			if (this.Creature.IsDead)
			{
				// Defaults
				this.Add(ReviveOptions.Town);
				this.Add(ReviveOptions.WaitForRescue);
				if (this.Creature.IsPet)
					this.Add(ReviveOptions.PhoenixFeather);

				// Dungeons
				if (this.Creature.Region is DungeonRegion)
				{
					this.Add(ReviveOptions.DungeonEntrance);

					// Show statue option only if there is a statue on this floor
					var floorRegion = (this.Creature.Region as DungeonFloorRegion);
					if (floorRegion == null || floorRegion.Floor.Statue)
						this.Add(ReviveOptions.StatueOfGoddess);
				}
				// Fields
				else
				{
					//if(creature.Exp > -90%)
					this.Add(ReviveOptions.Here);
				}

				// Special
				if (this.Creature.Titles.SelectedTitle == TitleId.devCAT)
					this.Add(ReviveOptions.HereNoPenalty);
			}

			if (this.Options != before || this.Creature.IsDead)
				Send.DeadFeather(this.Creature);
		}
	}
}
