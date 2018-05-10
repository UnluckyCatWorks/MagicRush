﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class Tutorial : MonoBehaviour
{
	#region DATA
	[Header ("Tuto")]
	public List<Supply> supplies;

	public static Tutorial manager;
	public static Dictionary<Phases, Check> Checks;
	public static bool onTutorial;
	#endregion

	#region CALLBACKS
	public void StartTutorial () 
	{
		StartCoroutine (Logic ());
	}

	private IEnumerator Logic () 
	{
		// Start tutorial
		onTutorial = true;
		Game.paused = false;

		#region PREPARATION
		// Get some references
		var menu = GameObject.Find ("UI_MENU").GetComponent<Animator> ();
		var focos = GameObject.Find ("Focos").GetComponent<Animator> ();
		var rig = GameObject.Find ("Camera_Rig").GetComponent<Animator> ();
		var modeMenu = GameObject.Find ("UI_MODE_SELECTION");

		// Create Players' Characters
		var ps = Character.SpawnPack ();
		// Position them
		var positions = Lobby.Get<Transform> ("Start_", false);
		ps[0].transform.position = positions[0].position;
		ps[1].transform.position = positions[1].position;
		// Restrict their capabilities
		ps.ForEach (p => p.AddCC ("Movement", Locks.Movement));
		ps.ForEach (p => p.AddCC ("Dash", Locks.Dash));
		ps.ForEach (p => p.AddCC ("Spells", Locks.Spells));
		ps.ForEach (p => p.AddCC ("Interaction", Locks.Interaction));

		// Spawn the Tuto_Icons on them
		var iconsPrefab = Resources.Load<TutoIcons> ("Prefabs/UI/Tuto_Icons");
		var icons = new List<TutoIcons>
		{
			Instantiate (iconsPrefab, ps[0].transform),
			Instantiate (iconsPrefab, ps[1].transform)
		};
		icons[0].InitializeAs (Player.all[0].scheme.type);
		icons[1].InitializeAs (Player.all[1].scheme.type);
		#endregion

		// Go to the scene
		rig.SetTrigger ("ToScene");
		focos.SetTrigger ("Light_Scene");
		StartCoroutine (Extensions.FadeAmbient (1.6f, 3f, 0.5f));
		// Wait until mode menu is out of camera
		yield return new WaitForSeconds (1.1f);
		modeMenu.SetActive (false);

		// Wait a bit
		yield return new WaitForSeconds (3f);

		#region MOVING
		Title.Show ("MOVE", 2f);

		// Show movement marks
		var markers = Lobby.Get<TutoPoint> ("Movement_", false);
		// Show correct icon (depends on input scheme)
		for (int i=0; i!=2; i++) 
		{
			int id = (int) Player.all[i].scheme.type;
			var child = markers[i].transform.GetChild (id+1);

			markers[i].marker.sign = child.GetComponent<SpriteRenderer> ();
			child.gameObject.SetActive (true);
		}
		// Assign observed characters
		markers[0].observedCharacter = ps[0].ID;
		markers[1].observedCharacter = ps[1].ID;
		// Turn them on
		markers[0].marker.On (ps[0].focusColor);
		markers[1].marker.On (ps[1].focusColor);

		// Alow movement
		ps.ForEach (p=> p.RemoveCC ("Movement"));

		// Wait until all players are in place
		Checks.Add (Phases.Moving, new Check ());
		while (!Checks[Phases.Moving].Ready) yield return null;
		Checks.Remove (Phases.Moving);

		// Turn off markers
		markers[0].marker.Off (TutoPoint.validColor);
		markers[0].marker.Off (ps[0].focusColor);
		Destroy (markers[0]);
		markers[1].marker.Off (TutoPoint.validColor);
		markers[1].marker.Off (ps[1].focusColor);
		Destroy (markers[1]);
		#endregion

		#region DASHING
		// Show water pit
		Game.paused = true;
		yield return new WaitForSeconds (1f);
		GameObject.Find ("Plat_agua").GetComponent<Animation> ().Play ("Out");
		GameObject.Find ("Plat_agua").GetComponentInChildren<Collider> ().enabled = false;

		// Wait a bit
		yield return new WaitForSeconds (1f);

		// Allow dashing
		ps.ForEach (p=> p.RemoveCC ("Dash"));
		Game.paused = false;

		// Show Dash marks
		markers = Lobby.Get<TutoPoint> ("Dash_", false);
		// Assign observed characters
		markers[0].observedCharacter = ps[0].ID;
		markers[1].observedCharacter = ps[1].ID;
		// Turn them on
		markers[0].marker.On (ps[0].focusColor);
		markers[1].marker.On (ps[1].focusColor);

		// Show icons
		icons.ForEach (i=> i.Show ("Dash"));
		Title.Show ("DASH", 2f);

		// Wait until all players are in place
		Checks.Add (Phases.Dashing, new Check ());
		while (!Checks[Phases.Dashing].Ready) yield return null;
		Checks.Remove (Phases.Dashing);

		// Turn off markers
		markers[0].marker.Off (TutoPoint.validColor);
		markers[0].marker.Off (ps[0].focusColor);
		Destroy (markers[0]);
		markers[1].marker.Off (TutoPoint.validColor);
		markers[1].marker.Off (ps[1].focusColor);
		Destroy (markers[1]);
		// Hide icons
		icons.ForEach (i=> i.Hide ("Dash"));

		// Hide water pit (play backwards)
		GameObject.Find ("Plat_agua").GetComponent<Animation> ().PlayInReverse ("Out");
		GameObject.Find ("Plat_agua").GetComponentInChildren<Collider> ().enabled = true;
		#endregion

		#region SPELL
		// Wait a bit
		yield return new WaitForSeconds (3f);
		Title.Show ("SPELLS", 2f);

		// Allow spells & show icons
		ps.ForEach (p=> p.RemoveCC ("Spells"));
		icons.ForEach (i=> i.Show ("Spell"));

		// Wait until all players have landed a spell
		Checks.Add (Phases.Casting_Spells, new Check ());
		while (!Checks[Phases.Casting_Spells].Ready) yield return null;
		Checks.Remove (Phases.Casting_Spells);

		// Hide icons
		yield return new WaitForSeconds (1f);
		icons.ForEach (i=> i.Hide ("Spell"));
		#endregion

		#region GRABBING / THROWING
		// Wait a bit
		yield return new WaitForSeconds (1f);
		Title.Show ("GRAB'N'HIT", 2f);
		yield return new WaitForSeconds (1f);

		// Show icons & allow interactions
		ps.ForEach (p=> p.RemoveCC ("Interaction"));
		icons.ForEach (i=> i.Show ("Interaction"));

		// Show supplies
		supplies.ForEach (s => 
		{
			// Appear with a 'Puff'
			s.gameObject.SetActive (true);
			var puff = Instantiate ((Game.manager as Lobby).puff);
			puff.transform.position = s.transform.position + Vector3.up * 0.5f;
			Destroy (puff.gameObject, 2f);
			puff.Play ();
		});

		Checks.Add (Phases.Throwing_Stuff, new Check ());
		while (!Checks[Phases.Throwing_Stuff].Ready) yield return null;
		Checks.Remove (Phases.Throwing_Stuff);

		yield return new WaitForSeconds (2f);
		#endregion

		Title.Show ("CONGRATULATIONS!", 2.5f);
		(Game.manager as Lobby).confetti.Play ();
		yield return new WaitForSeconds (3f);
		Title.Show ("YOU'RE READY FOR THE ARENA", 2f);
		yield return new WaitForSeconds (3f);

		#region RETURN
		// Return to Mode selection
		focos.SetTrigger ("Go_Off");
		rig.SetTrigger ("ToModeSelect");

		// Make supplies dissappear
		supplies.ForEach (s =>
		{
			// Dissappear with a 'Puff'
			s.gameObject.SetActive (false);
			var puff = Instantiate ((Game.manager as Lobby).puff);
			puff.transform.position = s.transform.position + Vector3.up * 0.5f;
			Destroy (puff.gameObject, 2f);
			puff.Play ();
		});
		// Make all grabbables dissapear
		FindObjectsOfType<Grabbable> ().ToList ().ForEach (g=>
		{
			// Destroy with a 'Puff'
			g.Destroy ();

			var puff = Instantiate ((Game.manager as Lobby).puff);
			puff.transform.position = g.transform.position + Vector3.up * 0.2f;
			Destroy (puff.gameObject, 2f);
			puff.Play ();
		});

		Extensions.FadeAmbient (2.9f, 2f, 5f);
		yield return new WaitForSeconds (1f);

		// Make players dissapear
		ps.ForEach (p =>
		{
			// Destroy with a 'Puff'
			Destroy (p.gameObject);

			var puff = Instantiate ((Game.manager as Lobby).puff);
			puff.transform.position = p.transform.position + Vector3.up * 0.5f;
			Destroy (puff.gameObject, 2f);
			puff.Play ();
		});

		// Show menu when out of camera
		yield return new WaitForSeconds (1.5f);
		modeMenu.SetActive (true);
		// Disable menu blocker
		yield return new WaitForSeconds (1f);
		modeMenu.transform.GetChild (0).gameObject.SetActive (true); 
		#endregion

		// End tutorial
		onTutorial = false;
	}

	private void Awake () 
	{
		manager = this;
		// Reset
		Checks = new Dictionary<Phases, Check> ();
		onTutorial = false;
	}
	#endregion

	#region HELPERS
	public enum Phases 
	{
		NONE,
		Moving,
		Dashing,
		Casting_Spells,
		Throwing_Stuff,
	}

	public class Check 
	{
		List<Characters> validatedCharacters = new List<Characters> (2);

		// Only true if all players who
		// are currently playing are done
		public bool Ready 
		{
			get
			{
				// For now, only 2 players can play at once
				return (validatedCharacters.Count == 2);
			}
		}

		// Keeps track of who has validated this point
		public void Set (Characters who, bool value)
		{
			// Validate character
			if (value && !validatedCharacters.Contains (who))
				validatedCharacters.Add (who);
			else
			// De-validate character
			if (!value && validatedCharacters.Contains (who))
				validatedCharacters.Remove (who);
		}
	}

	public static void SetCheckFor (Characters character, Phases phase, bool value) 
	{
		// Will check only if tutorial is on given Phase
		if (!onTutorial || !Checks.ContainsKey (phase)) return;
		// Set value
		Checks[phase].Set (character, value);
	}
	#endregion
}
