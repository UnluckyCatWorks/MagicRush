﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Holder : BInteractable
{
	private Collider col;
	public bool locked { get; private set; }

	[FlagEnum]
	public ObjectTypes validObjects;
	public Rigidbody obj;

	#region INTERACTION
	public override void Action (BCharacter player) 
	{
		// If player is dropping
		if (player.gobj != null)
		{
			// Drop object
			obj = player.gobj.body;
			player.gobj = null;
		}
		// If player is grabbing
		else
		{
			// Grab object
			obj.isKinematic = true;
			player.gobj = obj.GetComponent<BGrabbableObject>();
			obj = null;
		}
	}

	public override PlayerIsAbleTo CheckInteraction (BCharacter player) 
	{
		if (locked) return PlayerIsAbleTo.None;

		// If player is dropping
		if (player.gobj != null)
		{
			// Can't drop if another object is already in
			if (obj != null || !IsValidObject(player.gobj))
				return PlayerIsAbleTo.None;

			// If everything's fine
			return PlayerIsAbleTo.Action;
		}
		else
		{
			// Can't grab air
			if (obj == null) return PlayerIsAbleTo.None;

			// If everything's fine
			return PlayerIsAbleTo.Action;
		}
	}
	#endregion

	private void FixedUpdate () 
	{
		if (obj == null) return;
		// Make object follow Holder
		var newPos = Vector3.Lerp(obj.position, transform.position, Time.fixedDeltaTime * 7f);
		obj.MovePosition (newPos);
	}
	protected override void Awake ()
	{
		base.Awake ();
		col = GetComponent<Collider> ();
	}

	#region HELPERS
	public void Lock (){ locked = true; col.enabled = false; }
	public void Unlock () { locked = false; col.enabled = true; }

	[Flags]
	public enum ObjectTypes
	{
		RawIngredient = 1 << 0,
		ProcessedIngredient = 1 << 1,
		Potion = 1 << 2
	}

	public bool IsValidObject (BGrabbableObject obj) 
	{
		// If it's a potion
		if (validObjects.HasFlag(ObjectTypes.Potion) && obj is Potion)
			return true;

		// If it's a raw ingredient
		if (validObjects.HasFlag (ObjectTypes.RawIngredient))
		{
			var ingredient = obj as Ingredient;
			if (ingredient != null && ingredient.type == IngredientType.Raw)
				return true;
		}
		// If it's NOT a raw ingredient
		if (validObjects.HasFlag(ObjectTypes.ProcessedIngredient))
		{
			var ingredient = obj as Ingredient;
			if (ingredient != null && ingredient.type != IngredientType.Raw)
				return true;
		}
		// If none of above is valid
		return false;
	}
	#endregion
}
