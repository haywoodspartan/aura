//--- Aura Script -----------------------------------------------------------
// Alby Beginner
//--- Description -----------------------------------------------------------
// Easier version of Alby, accessible via Alby Beginner Pass.
//---------------------------------------------------------------------------

[DungeonScript("tircho_alby_beginner_1_dungeon")]
public class AlbyBeginnerDungeonScript : DungeonScript
{
	public override void OnBoss(Dungeon dungeon)
	{
		dungeon.AddBoss(30018, 1); // Giant Spiderling
		dungeon.AddBoss(30019, 3); // Red Spiderling

		dungeon.PlayCutscene("bossroom_GiantSpider_kid");
	}

	public override void OnCleared(Dungeon dungeon)
	{
		var rnd = RandomProvider.Get();
		var creators = dungeon.GetCreators();

		for (int i = 0; i < creators.Count; ++i)
		{
			var member = creators[i];
			var treasureChest = new TreasureChest();

			treasureChest.AddGold(rnd.Next(58, 86)); // Gold
			treasureChest.Add(GetRandomTreasureItem(rnd)); // Random item

			dungeon.AddChest(treasureChest);

			member.GiveItemWithEffect(Item.CreateKey(70028, "chest", member.EntityId));
		}
	}

	List<DropData> drops;
	public Item GetRandomTreasureItem(Random rnd)
	{
		if (drops == null)
		{
			drops = new List<DropData>();
			drops.Add(new DropData(itemId: 51001, chance: 35, amountMin: 5, amountMax: 10)); // HP 10 Potion
			drops.Add(new DropData(itemId: 51011, chance: 35, amountMin: 5, amountMax: 10)); // Stamina 10 Potion
		}

		return Item.GetRandomDrop(rnd, drops);
	}
}
