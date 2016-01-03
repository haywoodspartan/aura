//--- Aura Script -----------------------------------------------------------
// MeekBat AI
//--- Description -----------------------------------------------------------
// AI for MeekBats.
//---------------------------------------------------------------------------

[AiScript("meekbat")]
public class MeekBatAi : AiScript
{
	public MeekBatAi()
	{
		Doubts("/pc/", "/pet/");
		SetAggroRadius(200); // 120 angle 1000 audio

		On(AiState.Aggro, AiEvent.DefenseHit, OnDefenseHit);
		On(AiState.Aggro, AiEvent.Hit, OnHit);
	}

	protected override IEnumerable Idle()
	{
		Do(Wander(100, 800));
		Do(Wait(2000, 5000));
	}

	protected override IEnumerable Aggro()
	{
		Do(Wait(5000));
		Do(Attack(3));
		Do(Wait(3000));
	}

	private IEnumerable OnDefenseHit()
	{
		Do(Attack());
		Do(Wait(3000));
	}

	private IEnumerable OnHit()
	{
		var rndOH = Random();
		if (rndOH < 15)
		{
			Do(KeepDistance(1000, false, 2000));
		}
		else if (rndOH < 30)
		{
			Do(Timeout(2000, Wander(100, 500, false)));
		}
		else
		{
			Do(Attack(3, 4000));
		}
	}
}
