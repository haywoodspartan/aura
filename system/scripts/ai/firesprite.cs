//--- Aura Script -----------------------------------------------------------
// Fire Sprite AI
//--- Description -----------------------------------------------------------
// AI for Fire Sprites.
//---------------------------------------------------------------------------

[AiScript("firesprite")]
public class FireSpriteAi : AiScript
{
	public FireSpriteAi()
	{
		Hates("/pc/", "/pet/");
		//HatesAttacking("/elemental/");

		On(AiState.Aggro, AiEvent.Hit, OnHit);
		On(AiState.Aggro, AiEvent.KnockDown, OnKnockDown);
		On(AiState.Aggro, AiEvent.DefenseHit, OnDefenseHit);
	}

	protected override IEnumerable Idle()
	{
		if (Random() < 50)
			Do(Wander(300, 500, false));
		else
			Do(Wait(2000, 5000));
	}

	protected override IEnumerable Alert()
	{
		var num = Random();
		if (num < 20) // 20%
		{
			Do(Say("?"));
			Do(Wait(1000, 2000));
		}
		else if (num < 50) // 30%
		{
			Do(Say("?"));
			Do(Wait(1000, 4000));

			Do(Say("..."));
			Do(Circle(600, 2000, 2000));
		}

		Do(Say("!!!"));
		Do(PrepareSkill(SkillId.Firebolt)); // TODO: Stacks 1|2
		Do(Wait(2000, 10000));
	}

	protected override IEnumerable Aggro()
	{
		var num = Random();
		if (num < 10) // 10%
		{
			if (Random() < 50)
				Do(Wander(100, 200, false));

			Do(Say("!"));
			Do(Attack(3, 4000));

			num = Random();
			if (num < 60) // 60%
			{
				Do(Say("!!!"));
				Do(PrepareSkill(SkillId.Firebolt));

			}
			else if (num < 80) // 20%
			{
				Do(Say("!!"));
				Do(PrepareSkill(SkillId.Defense));
				Do(Follow(50, true, 1000));
			}
			Do(Wait(500, 2000));
		}
		else if (num < 20) // 10%
		{
			Do(PrepareSkill(SkillId.Defense));
			Do(Say("!!"));

			if (Random() < 60)
				Do(Circle(400, 2000, 2000, false));
			else
				Do(Follow(400, true, 5000));

			Do(CancelSkill());
		}
		else if (num < 30) // 10%
		{
			num = Random();
			if (num < 60) // 60%
				Do(Circle(400, 2000, 2000, false));
			else if (num < 80) // 20%
				Do(Follow(400, false, 5000));
			else // 20%
				Do(KeepDistance(1000, false, 5000));
		}
		else // 70%
		{
			Do(Say("!!!"));
			Do(PrepareSkill(SkillId.Firebolt));
			Do(Say("!"));
			Do(Attack(1, 4000));
			Do(Attack(2, 4000));

			if (Random() < 40)
			{
				if (Random() < 50)
					Do(PrepareSkill(SkillId.Firebolt)); // 2 stacks
				else
					Do(PrepareSkill(SkillId.Firebolt)); // 3 stacks
				Do(Attack(1, 4000));
				Do(Attack(2, 4000));
			}
		}
	}

	private IEnumerable OnHit()
	{
		if (Random() < 40)
			Do(KeepDistance(1000, false, 2000));
		else
			Do(Attack(3, 4000));
	}

	private IEnumerable OnKnockDown()
	{
		if (Random() < 50)
		{
			Do(PrepareSkill(SkillId.Defense));
			Do(Say("!!"));
			if (Random() < 60)
				Do(Circle(400, 2000, 2000));
			else
				Do(Follow(400, true, 5000));
			Do(CancelSkill());
		}
		else
		{
			Do(Say("!"));
			Do(Attack(3, 8000));
			Do(Say("!!!"));
		}
	}

	private IEnumerable OnDefenseHit()
	{
		Do(Say("?!?!"));
		Do(Attack(3, 4000));

		if (Random() < 40)
		{
			Do(Say("!"));
			Do(PrepareSkill(SkillId.Firebolt));
			Do(Wait(1000, 2000));
			Do(KeepDistance(1000, false, 2000));
		}
	}
}
