//--- Aura Script -----------------------------------------------------------
// G1 019: Magic Powder of Preservation
//--- Description -----------------------------------------------------------
// Getting Magic Powder of Preservation from Fiodh.
// 
// See "gairech_fiodh_dungeon" for retrieving the powder.
// 
// Wiki:
// - Find the powder at Fiodh Dungeon.
//---------------------------------------------------------------------------

public class MagicPowderOfPreservationQuest : QuestScript
{
	private const int Powder = 73061;
	private const int Glasses = 73004;

	public override void Load()
	{
		SetId(210024);
		SetName(L("Obtain the Magic Powder of Preservation"));
		SetDescription(L("Bring me the Magic Powder of Preservation, so I can prevent the glasses from breaking further. - Tarlach -"));

		SetIcon(QuestIcon.AdventOfTheGoddess);
		if (IsEnabled("QuestViewRenewal"))
			SetCategory(QuestCategory.AdventOfTheGoddess);

		AddObjective("give_powder", L("Give Tarlach the Magic Powder of Preservation."), 48, 11100, 30400, Talk("tarlach"));

		AddReward(Exp(455));
		AddReward(Gold(650));
		AddReward(Item(Glasses));
		AddReward(WarpScroll(63009, "rabbie_dungeon"));

		AddHook("_tarlach", "after_intro", TarlachAfterIntro);
	}

	public async Task<HookResult> TarlachAfterIntro(NpcScript npc, params object[] args)
	{
		if (npc.QuestActive(this.Id, "get_powder") && npc.HasItem(Powder))
		{
			npc.CompleteQuest(this.Id);

			npc.RemoveKeyword("g1_28");
			npc.GiveKeyword("g1_29");

			npc.RemoveItem(Powder);

			npc.Msg(L("Oh, you found it.<br/>I will put this powder on my glasses right now."));
			npc.Msg(L("This is a memory item that contains the preserved memory I had of that time."), npc.Image("g1_ch25_glasses"));
			npc.Msg(L("I will now cast a magic spell on the item with the magic powder of preservation."));
			npc.Msg(L("...And this is the Red Wing of the Goddess<br/>which will take you to the Rabbie Dungeon...<br/>Go to Rabbie Dungeon and put these glasses on the altar."));
			npc.Msg(L("Then... you will know about...<br/>my anger toward the Goddess and evil spirits..."));

			return HookResult.Break;
		}

		return HookResult.Continue;
	}
}
