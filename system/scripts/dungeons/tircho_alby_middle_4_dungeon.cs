//--- Aura Script -----------------------------------------------------------
// Alby Int 4
//--- Description -----------------------------------------------------------
// Script for Alby Intermediate for Four.
//---------------------------------------------------------------------------

[DungeonScript("tircho_alby_middle_4_dungeon")]
public class AlbyIntFourDungeonScript : DungeonScript
{
	public override void OnBoss(Dungeon dungeon)
	{
		dungeon.AddBoss(30009, 1); // Giant Black Spider
		dungeon.AddBoss(30010, 1); // Giant Red Spider
		dungeon.AddBoss(30011, 1); // Giant White Spider
		dungeon.AddBoss(30012, 16); // Burgundy Spider

		dungeon.PlayCutscene("bossroom_giant_spiderRBW");
	}

	public override void OnCleared(Dungeon dungeon)
	{
		var rnd = RandomProvider.Get();
		var creators = dungeon.GetCreators();

		for (int i = 0; i < creators.Count; ++i)
		{
			var member = creators[i];
			var treasureChest = new TreasureChest();

			if (i == 0)
			{
				// Enchanted item
				Item item = null;
				switch (rnd.Next(2))
				{
					case 0: item = Item.CreateEnchanted(40026, prefix: 20404); break; // Sacrificial Sickle
					case 1: item = Item.CreateEnchanted(18024, prefix: 30411); break; // Magnolia Hairband
					case 2: item = Item.CreateEnchanted(40003, prefix: 30514); break; // Hope Short Bow
				}
				treasureChest.Add(item);
			}

			treasureChest.AddGold(rnd.Next(4488, 8160)); // Gold
			treasureChest.Add(GetRandomTreasureItem(rnd)); // Random item

			dungeon.AddChest(treasureChest);

			member.GiveItemWithEffect(Item.CreateKey(70028, "chest"));
		}
	}

	List<DropData> drops;
	public Item GetRandomTreasureItem(Random rnd)
	{
		if (drops == null)
		{
			drops = new List<DropData>();
			drops.Add(new DropData(itemId: 62004, chance: 4, amountMin: 2, amountMax: 4)); // Magic Powder
			drops.Add(new DropData(itemId: 51102, chance: 4, amountMin: 2, amountMax: 4)); // Mana Herb
			drops.Add(new DropData(itemId: 51013, chance: 5, amountMin: 2, amountMax: 4)); // Stamina 50 Potion
			drops.Add(new DropData(itemId: 51008, chance: 5, amountMin: 2, amountMax: 4)); // MP 50 Potion
			drops.Add(new DropData(itemId: 51003, chance: 5, amountMin: 2, amountMax: 4)); // HP 50 Potion
			drops.Add(new DropData(itemId: 63116, chance: 5, amount: 1, expires: 480)); // Alby Intermediate Fomor Pass for One
			drops.Add(new DropData(itemId: 63117, chance: 3, amount: 1, expires: 480)); // Alby Intermediate Fomor Pass for Two
			drops.Add(new DropData(itemId: 63118, chance: 3, amount: 1, expires: 480)); // Alby Intermediate Fomor Pass for Four

			if (IsEnabled("AlbyAdvanced"))
			{
				drops.Add(new DropData(itemId: 63160, chance: 2, amount: 1, expires: 360)); // Alby Advanced 3-person Fomor Pass
				drops.Add(new DropData(itemId: 63161, chance: 1, amount: 1, expires: 360)); // Alby Advanced Fomor Pass
			}
		}

		return Item.GetRandomDrop(rnd, drops);
	}
}
