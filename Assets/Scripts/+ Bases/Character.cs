﻿using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
public abstract class Character : Pawn
{
	#region DATA
	[Header ("Basic settings")]
	public Transform grabHandle;
	public Characters ID;
	public Color focusColor;
	public float crystalEmission;

	[Header ("Spell settings")]
	[ColorUsage(true, true, 0, 8, 0.125f, 3)]
	public Color areaColor;
	public float spellCooldown;

	// Internal info
	protected Dictionary<string, Locks> effects;
	internal Locks locks;

	protected SmartAnimator anim;
	protected CharacterController me;
	protected Character other;

	protected Marker areaOfEffect;
	protected SphereCollider areaCollider;

	internal Material mat;
	internal int _EmissionColor;

	// Locomotion
	internal float speed = 8.5f;
	internal float angularSpeed = 120f;
	internal float gravityMul = 1f;

	// Control
	protected Quaternion targetRotation;
	internal Vector3 movingSpeed;
	internal Vector3 MovingDir 
	{
		get
		{
			return (movingSpeed == Vector3.zero) ?
				transform.forward : movingSpeed.normalized;
		}
	}

	internal CollisionFlags collision;
	private Vector3 lastAlivePos;

	// Capabilities
	internal static float ThrowForce = 20f;

	internal static float DashForce = 40f;
	internal static float DashCooldown = 0.50f;
	protected const float DashDuration = 0.25f;
	protected Coroutine dashCoroutine;

	internal bool knocked;
	internal Coroutine knockedCoroutine;

	protected const float spellSelfStun = 0.50f;
	protected Coroutine spellCoroutine;

	protected Interactable lastMarked;
	internal Grabbable toy;

	// Animation
	public bool Moving 
	{
		get { return anim.GetBool ("Moving"); }
		set { anim.SetBool ("Moving", value); }
	}
	public bool Dashing 
	{
		get { return anim.GetBool ("Dashing"); }
		set { anim.SetBool ("Dashing", value); }
	}
	public bool Casting 
	{
		get { return anim.GetBool ("Casting_Spell"); }
		set { anim.SetBool ("Casting_Spell", value); }
	}
	public bool Carrying 
	{
		get { return anim.GetBool ("Carrying_Stuff"); }
		set { anim.SetBool ("Carrying_Stuff", value); }
	}
	#endregion

	#region LOCOMOTION
	private void Movement () 
	{
		// Get input
		var input = Vector3.zero;
		input.x = Owner.GetAxis ("Horizontal");
		input.z = Owner.GetAxis ("Vertical");

		// Compute rotation equivalent to moving direction
		var dir = Vector3.Min (input, input.normalized);
		if (input != Vector3.zero)
		{
			// Transform direction to be camera-dependent
			dir = TranformToCamera (dir);
			targetRotation = Quaternion.LookRotation (dir);
		}

		// Modify speed to move character
		if (!locks.HasFlag (Locks.Movement))
		{
			movingSpeed = dir * speed;
			// Activate moving animations
			if (input != Vector3.zero) Moving = true;
			else Moving = false;
		}
		else Moving = false;
	}

	private void Rotation () 
	{
		if (locks.HasFlag (Locks.Rotation)) return;

		// Rotate character towards moving directions
		var factor = angularSpeed * Time.deltaTime;
		var newRot = Quaternion.Slerp (transform.rotation, targetRotation, factor);
		transform.rotation = newRot;
	}

	// Actual movement is held here
	private void Move () 
	{
		// Apply gravity
		var gravity = Physics.gravity * gravityMul;
		var finalSpeed = movingSpeed + gravity;

		// If bewitched
		if (locks.HasFlag (Locks.Crazy))
		{
			// Invert speed
			finalSpeed.x *= -1;
			finalSpeed.z *= -1;
		}

		// Move player
		collision = me.Move (finalSpeed * Time.deltaTime);
		TrackPosition ();
	}
	#endregion

	#region DEATH TRACKING
	private void TrackPosition () 
	{
		if (collision.HasFlag (CollisionFlags.Below))
			lastAlivePos = transform.position;
	}

	public void Respawn () 
	{
		transform.position = lastAlivePos;
	}
	#endregion

	#region TOY
	// Keeps toy with the character
	private void HoldToy () 
	{
		if (toy == null) 
		{
			Carrying = false;
			return;
		}
		else Carrying = true;

		// Make toy follow smoothly
		var newPos = Vector3.Lerp (toy.body.position, grabHandle.position, Time.fixedDeltaTime * 7f);
		toy.body.MovePosition (newPos);
	}
	#endregion

	#region DASHING
	private void Dash () 
	{
		if (locks.HasFlag (Locks.Dash)) return;         // Is Dash up?
		else if (!Owner.GetButton ("Dash")) return;     // Has user pressed the button?
		else if (toy) return;                           // Can't dash while holding stuff
		else if (Dashing) return;						// Can't dash if already dashing
		// If everything's ok
		else Dashing = true;

		// Start dash & put in cooldoown
		AddCC ("Dashing", Locks.Locomotion);
		dashCoroutine = StartCoroutine (InDash ());
	}

	private IEnumerator InDash () 
	{
		// Cache gravity & reduce its
		float oldGravity = gravityMul;
		gravityMul = 0.6f;

		float factor = 0f;
		bool knockOcurred = false;
		while (factor <= 1.1f) 
		{
			// Move player at dash speed (slow as closer to end)
			movingSpeed = MovingDir * DashForce * (1f - factor);

			// Knock other character back if hit
			var dist = Vector3.Distance (transform.position, other.transform.position);
			if (dist <= 0.8 && !knockOcurred)
			{
				// Get force from movement & supress Y-force
				other.Knock (MovingDir, 0.25f);

				// Hard-slow dash & avoid knocking again
				knockOcurred = true;
				factor = 0.6f;

			}

			factor += Time.deltaTime / DashDuration;
			yield return null;
		}
		// Reset
		RemoveCC ("Dashing");
		Dashing = false;

		// Restore gravity
		gravityMul = oldGravity;
	}
	#endregion

	#region KNOCKING
	public void Knock (Vector3 dir, float duration) 
	{
		// Only add CC if wasn't already knocked
		if (!effects.ContainsKey ("Knocked")) AddCC ("Knocked", Locks.All, interrupt: Locks.Dash | Locks.Spells);
		else if (knockedCoroutine != null) StopCoroutine (knockedCoroutine);

		knockedCoroutine = StartCoroutine (KnockingTo (dir, duration));

		// Let go grabbed object, if any,
		// in opposite direction of knock
		if (toy) toy.Throw (-dir * 5f, owner: this);
	}

	private IEnumerator KnockingTo (Vector3 dir, float duration) 
	{
		// Supress vertical force
		dir.y = 0f;

		var factor = 0f;
		while (factor <= 1f)
		{
			// Move player during knock
			movingSpeed = dir * DashForce * (1f - factor);

			// Rotate player 'cause its cool
			transform.Rotate (Vector3.up, 771f * Time.deltaTime);
			targetRotation = transform.rotation;

			factor += Time.deltaTime / duration;
			yield return null;
		}
		yield return new WaitForSeconds (0.1f);

		// Reset
		effects.Remove ("Knocked");
		knocked = false;
	}
	#endregion

	#region SPELL CASTING
	private void CheckSpell () 
	{
		if (locks.HasFlag (Locks.Spells)) return;
		if (!Owner.GetButton ("Spell")) return;
		if (toy) return;
		// If everything's ok
		else Casting = true;
		var block = (Locks.Locomotion | Locks.Interaction);
		AddCC ("Spell Casting", block, Locks.NONE, spellSelfStun);

		// Self CC used as cooldown
		AddCC ("-> Spell", Locks.Spells);
		SwitchCrystal (value: false);
		StartCoroutine (WaitSpellCD ());

		// Cast spell & put it on CD
		spellCoroutine = StartCoroutine (CastSpell ());
	}

	private IEnumerator CastSpell ()
	{
		areaOfEffect.On (areaColor);                            // Show area
		yield return new WaitForSeconds (spellSelfStun);        // Allow spell aiming while self-stunned
		areaOfEffect.Off ();                                    // Hide area

		// Set animator state
		Casting = false;
		// Always call this, even if there's no hit
		BeforeSpell ();

		// If other player was hit
		var hits = Physics.OverlapSphere (areaOfEffect.transform.position, areaCollider.radius, 1<<14);
		if (hits.Any (c=> c.name == other.name))
		{
			// If inside tutorial 
			Tutorial.SetCheckFor (ID, Tutorial.Phases.Casting_Spells, true);

			// Actually do stuff
			SpellEffect ();
		}
	}

	protected abstract void SpellEffect ();
	protected virtual void BeforeSpell () {/* This is always called */ }

	private IEnumerator WaitSpellCD () 
	{
		// Just wait until CD is over
		yield return new WaitForSeconds (spellCooldown);
		SwitchCrystal (value: true);
		RemoveCC ("-> Spell");
	}

	public void SwitchCrystal (bool value) 
	{
		var color = Color.white * (value? crystalEmission : 0f);
		mat.SetColor (_EmissionColor, color);
	}
	#endregion

	#region INTERACTION
	private void CheckInteractions () 
	{
		if (locks.HasFlag (Locks.Interaction)) return;

		var ray = NewRay ();
		var hit = new RaycastHit ();
		if (Physics.Raycast (ray, out hit, 2f, 1 << 8 | 1 << 10))
		{
			// If valid target
			var interactable = hit.collider.GetComponent<Interactable> ();
			if (interactable && interactable.CheckInteraction (this))
			{
				if (!lastMarked)
				{
					// Focus object
					interactable.marker.On (focusColor);
					lastMarked = interactable;

					// Register player if it's a machine
					var m = interactable as MachineInterface;
					if (m) m.PlayerIsNear (near: true);
				}
				if (Owner.GetButton ("Action")) interactable.Action (this);
				return;
			}
		}

		// If not in front of any interactable
		// de-mark last one seen, if any
		if (lastMarked)
		{
			// Un-register player if it's a machine
			var m = lastMarked as MachineInterface;
			if (m) m.PlayerIsNear (near: false);

			// Un-focus
			lastMarked.marker.Off (focusColor);
			lastMarked = null;
		}

		// If not in front of any interactable,
		// or not executed any interaction
		if (Owner.GetButton ("Action", true) && toy)
			toy.Throw (MovingDir * ThrowForce, owner: this);
	}

	#endregion

	#region EFFECTS MANAGEMENT
	private void ReadEffects () 
	{
		// Check if spells were originally blocked
		bool spellsWereBlocked = locks.HasFlag (Locks.Spells);

		locks = Locks.NONE;
		foreach (var e in effects)
		{
			// Resets CCs & then reads them every frame
			locks = locks.SetFlag (e.Value);
		}
		// Check if spells are NOW originally blocked
		bool spellsAreBlocked = locks.HasFlag (Locks.Spells);
		if (spellsWereBlocked && !spellsAreBlocked)
			SwitchCrystal (value: true);
	}

	// Helper for only adding CCs
	public void AddCC (string name, Locks cc, Locks interrupt = Locks.NONE, float duration = 0) 
	{
		effects.Add (name, cc);
		if (duration != 0) StartCoroutine (RemoveEffectAfter (name, duration));

		// Interrupt any kind of movement
		if (cc.HasFlag (Locks.Movement)) 
		{
			movingSpeed = Vector3.zero;
			if (interrupt.HasFlag (Locks.Dash) &&
				dashCoroutine != null)
			{
				// Reset dashing
				RemoveCC ("Dashing");
				Dashing = false;

				StopCoroutine (dashCoroutine);
				dashCoroutine = null;
			}
		}
		else
		// Interrupt spell casting
		if (interrupt.HasFlag (Locks.Spells) &&
			spellCoroutine != null) 
		{
			Casting = false;
			SwitchCrystal (value: false);
			StopCoroutine (spellCoroutine);
			spellCoroutine = null;
		}
	}

	public void RemoveCC (string name) 
	{
		effects.Remove (name);
	}

	// Internal helper for temporal CCs
	IEnumerator RemoveEffectAfter (string name, float delay) 
	{
		yield return new WaitForSeconds (delay);
		RemoveCC (name);
	}
	#endregion

	#region HELPERS
	private Vector3 TranformToCamera (Vector3 dir) 
	{
		// Get coorrect camera-dependent vector
		var cam = Camera.main.transform;
		// Ignore rotation (except Y) 
		var rot = cam.eulerAngles;
		rot.x = 0;
		rot.z = 0;
		#warning really bad practice man
		return Matrix4x4.TRS (cam.position, Quaternion.Euler (rot), Vector3.one).MultiplyVector (dir);
	}

	private Ray NewRay () 
	{
		// Returns 'Ray' for checking interactions
		var origin = transform.position;
		origin.y += 0.75f + 0.15f;
		return new Ray (origin, transform.forward);
	}

	public static List<Character> SpawnPack () 
	{
		// Spawn
		var list = new List<Character>
		{
			Instantiate(Resources.Load<Character>("Prefabs/Characters/" + Player.all[0].playingAs)),
			Instantiate(Resources.Load<Character>("Prefabs/Characters/" + Player.all[1].playingAs)),
		};
		// Correct names
		list.ForEach (p => p.name = p.name.Replace ("(Clone)", string.Empty));

		// Assign them their owners
		list[0].ownerID = 1;
		list[1].ownerID = 2;
		// Assing other
		list[0].other = list[1];
		list[1].other = list[0];

		// Return
		return list;
	}

	public void FindOther () 
	{
		var list = FindObjectsOfType<Character> ().ToList ();
		// Find first other character in the scene
		if (list != null && list.Count > 1)
			other = list.FirstOrDefault (c=> c != this);
	}

	public static Character Get (Characters character) 
	{
		// Gets instance of specific character
		var c = GameObject.Find (character.ToString ());
		return c.GetComponent<Character> ();
	}
	#endregion

	#region UNITY CALLBACKS
	protected virtual void Update () 
	{
		if (Owner == null)
			return;

		// Initialization
		Owner.ResetInputs ();
		ReadEffects ();

		if (Game.paused) 
		{
			Moving = false;
			return;
		}

		// Locomotion
		Movement ();
		Rotation ();
		Dash ();
		Move ();

		// Interaction
		CheckInteractions ();
		CheckSpell ();
	}

	protected virtual void Awake () 
	{
		// Initialize stuff
		anim = new SmartAnimator (GetComponent<Animator> ());
		effects = new Dictionary<string, Locks> ();

		// Initialize crystal
		mat = GetComponentInChildren<Renderer> ().sharedMaterial;
		_EmissionColor = Shader.PropertyToID ("_EmissionColor");
		SwitchCrystal (value: true);

		// Get some references
		me = GetComponent<CharacterController> ();
		areaOfEffect = GetComponentInChildren<Marker> ();
		areaCollider = areaOfEffect.GetComponent<SphereCollider> ();
		targetRotation = transform.rotation;

		// Find other playera
		FindOther ();
	}

	protected virtual void FixedUpdate () 
	{
		HoldToy ();
	}
	#endregion
}
