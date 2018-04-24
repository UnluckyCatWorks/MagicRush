﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutoPoint : MonoBehaviour
{
	public string checkPoint;
	public string observedCharacter;

	private void OnTriggerEnter (Collider other) 
	{
		if (other.name != observedCharacter) return;
		if (TutorialGame.Checks.ContainsKey (checkPoint))
		{
			GetComponent<Marker> ().On (4, bypass: true);
			TutorialGame.Checks[checkPoint]++;
		}
	}
	private void OnTriggerExit (Collider other) 
	{
		if (other.name != observedCharacter) return;
		if (TutorialGame.Checks.ContainsKey (checkPoint))
		{
			GetComponent<Marker> ().Off (other.name.Contains ("Alby")? 1 : 2, bypass: true);
			TutorialGame.Checks[checkPoint]--;
		}
	}
}