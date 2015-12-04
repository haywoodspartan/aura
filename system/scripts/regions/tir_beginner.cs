//--- Aura Script -----------------------------------------------------------
// Tir Chonaill - Beginner Area (125) (Forest of Souls)
//--- Description -----------------------------------------------------------
// Region you are warped to after talking to Nao/Tin.
//---------------------------------------------------------------------------

public class TirBeginnerRegionScript : RegionScript
{
	public override void LoadWarps()
	{
		// Tir
		SetPropBehavior(0x00A0007D00060018, PropWarp(125,27753,72762, 1, 15250, 38467));
		
		// Gargoyles
		SetPropBehavior(0x00A0007D0001003A, PropWarp(125,19971,69993, 125,17186,69763));
		SetPropBehavior(0x00A0007D0001003B, PropWarp(125,17641,69874, 125,20453,70023));
	}
	
	public override void LoadSpawns()
	{
		// ...
	}

	public override void LoadEvents()
	{
		// "Altar" near Tin
		OnClientEvent("Tin_Beginner_Tutorial/_Tin_Beginner_Tutorial_01/tuto_start", SignalType.Enter, (creature, eventData) =>
		{
			// Only do this once.
			if (creature.Keywords.Has("tin_tutorial_guide"))
				return;

			if (!creature.Quests.Has(202001))
				creature.Quests.Start(202001, false); // Nao's Letter of Introduction

			Cutscene.Play("tuto_meet_tin", creature, (scene) =>
			{
				// Give first weapon
				if(creature.RightHand == null)
				{
					//if(!eiry)
					//	creature.Inventory.Add(40005, Pocket.RightHand1); // Short Sword
					//else
					{
						// Eiry Practice Short Sword
						creature.Inventory.AddWithUpdate(Item.CreateEgo(40524, EgoRace.EirySword, "Eiry"), Pocket.RightHand1);
					}
				}

				// Give as soon as the player got everything
				creature.Keywords.Give("tin_tutorial_guide");

				// Required to remove the fade effect.
				scene.Leader.Warp(125, 22930, 75423);
			});
		});
	}
}
