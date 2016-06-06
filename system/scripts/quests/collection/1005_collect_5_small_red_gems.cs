//--- Aura Script -----------------------------------------------------------
// Collect 5 Small Red Gems
//--- Description -----------------------------------------------------------
// Collection quest for Small Gems.
//---------------------------------------------------------------------------

public class Collect5SmallRedGems2QuestScript : QuestScript
{
	public override void Load()
	{
		SetId(1005);
		SetScrollId(70023);
		SetName(L("Collect Small Red Gems"));
		SetDescription(L("Please [collect 5 Small Red Gems]. The Imps have hidden them all over town. You'll find these gems if you [check suspicious items] around town, but you can also trade other gems to get Red Gems."));
		SetType(QuestType.Collect);

		AddObjective("collect1", L("Collect 5 Red Gems"), 0, 0, 0, Collect(52006, 5));

		AddReward(Exp(50));
		AddReward(Gold(200));
	}
}
