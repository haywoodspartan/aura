//--- Aura Script -----------------------------------------------------------
// Spirit Blunt (F) 
//--- Description -----------------------------------------------------------
// Female blunt ego
//---------------------------------------------------------------------------

public class SpiritBluntFScript : NpcScript
{
	public override void Load()
	{
		SetRace(1);
		SetName("_ego_female_blunt");
		SetLocation(22, 5800, 7100, 0);
	}

	protected override async Task Talk()
	{
		while (true)
		{
			Msg("How are you doing?", Button("Talk", "@talk"), Button("Give Item", "@feed_item"), Button("Repair", "@repair"), Button("Finish Conversation", "@endconvo"));
			var reply = await Select();

			if (reply == "@endconvo")
				break;

			Msg("(Unimplemented)");
		}

		Msg(Expression("good"), "See you another time.");
	}

	protected override async Task Keywords(string keyword)
	{
		switch (keyword)
		{
			default:
				RndFavorMsg(
					"..."
				);

				break;
		}
	}
}
