//--- Aura Script -----------------------------------------------------------
// Dog AI
//--- Description -----------------------------------------------------------
// AI for shepherd dogs.
//---------------------------------------------------------------------------

[AiScript("dog")]
public class DogAi : AiScript
{
	public DogAi()
	{
		SetVisualField(600, 90);
		SetAggroRadius(400);

		Hates("/wolf/");
	}

	protected override IEnumerable Idle()
	{
		Do(Wander());
		Do(Wait(8000));
		Do(StartSkill(SkillId.Rest));
		Do(Wait(2000, 30000));
		Do(StopSkill(SkillId.Rest));
	}

	protected override IEnumerable Aggro()
	{
		if (Random() < 50)
			Do(Attack(3));
		else
			Do(Circle(400, 1000, 3000));
		Do(Wait(3000));
	}
}
