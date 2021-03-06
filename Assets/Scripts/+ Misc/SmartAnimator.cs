﻿/// Written by @marsh12th
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmartAnimator : UnityEngine.Object
{
	#region DATA
	public Animator Animator { get; private set; }
	private Dictionary<string, Param<float>> floats;
	private Dictionary<string, Param<bool>> bools;
	private Dictionary<string, Param<int>> ints;
	private Dictionary<string, int> triggers;

	private class Param<T> where T : struct
	{
		public T value;
		public int hash;
		public bool isDrivenByCurve;

		public static implicit operator T (Param<T> param) 
		{ return param.value; }
	}
	#endregion

	/// <summary>
	/// Creates a wrapper around given Animator and makes a cache of all
	/// its parameters for faster checks and modifications.
	/// </summary>
	public SmartAnimator (Animator animator) 
	{
		// Null check
		if (!animator) Debug.LogWarning ("Smart Animator: Provided Animator is null!", this);

		// Initialize dictionaries
		floats = new Dictionary<string, Param<float>> ();
		bools = new Dictionary<string, Param<bool>> ();
		ints = new Dictionary<string, Param<int>> ();
		triggers = new Dictionary<string, int> ();

		Animator = animator;
		// Loops through animator parameters
		// Default (inspector) values are stored in the cache
		// now and then updated with each Set function.
		// This way we never have to call Animator.Get
		foreach (var p in animator.parameters)  
		{
			if (p.type == AnimatorControllerParameterType.Float)
			{
				var drivenByCurve = animator.IsParameterControlledByCurve (p.nameHash);
				var param = new Param<float> { hash=p.nameHash, value=p.defaultFloat, isDrivenByCurve=drivenByCurve };
				floats.Add (p.name, param);
			}
			else
			if (p.type == AnimatorControllerParameterType.Bool)
			{
				var drivenByCurve = animator.IsParameterControlledByCurve (p.nameHash);
				var param = new Param<bool> { hash=p.nameHash, value=p.defaultBool, isDrivenByCurve=drivenByCurve };
				bools.Add (p.name, param);
			}
			else
			if (p.type == AnimatorControllerParameterType.Int)
			{
				var drivenByCurve = animator.IsParameterControlledByCurve (p.nameHash);
				var param = new Param<int> { hash=p.nameHash, value=p.defaultInt, isDrivenByCurve=drivenByCurve };
				ints.Add (p.name, param);
			}
			else
			if (p.type == AnimatorControllerParameterType.Trigger) triggers.Add (p.name, p.nameHash);
		}
	}

	#region GETTERS
	/* Find parameter by given name and return its value.
	 * (parameters driven by curves aren't stored in cache) */

	public float GetFloat (string id) 
	{
		Param<float> cache;
		if (floats.TryGetValue (id, out cache))
		{
			if (cache.isDrivenByCurve) return Animator.GetFloat (cache.hash);
			else return cache;
		}
		else
		{
			Debug.LogError ("Can't find parameter, returning -1", this);
			return -1f;
		}
	}
	public bool GetBool (string id) 
	{
		Param<bool> cache;
		if (bools.TryGetValue (id, out cache))
		{
			if (cache.isDrivenByCurve) return Animator.GetBool (cache.hash);
			else return cache;
		}
		else
		{
			Debug.LogError ("Can't find parameter, returning false", this);
			return false;
		}
	}
	public int GetInt (string id) 
	{
		Param<int> cache;
		if (ints.TryGetValue (id, out cache))
		{
			if (cache.isDrivenByCurve) return Animator.GetInteger (cache.hash);
			else return cache;
		}
		else
		{
			Debug.LogError ("Can't find parameter, returning -1", this);
			return -1;
		}
	}
	#endregion

	#region SETTERS
	/* Find parameter by given name and set its value.
	 * Then update cache to avoid later checks. */

	public void SetFloat (string id, float value) 
	{
		Param<float> cache;
		if (floats.TryGetValue (id, out cache))
		{
			if (cache != value)
			{
				Animator.SetFloat (cache.hash, value);
				cache.value = value;
			}
		}
		else Debug.LogError ("Can't find parameter!", this);
	}
	public void SetBool (string id, bool value) 
	{
		Param<bool> cache;
		if (bools.TryGetValue (id, out cache))
		{
			if (cache != value)
			{
				Animator.SetBool (cache.hash, value);
				cache.value = value;
			}
		}
		else Debug.LogError ("Can't find parameter!", this);
	}
	public void SetInt (string id, int value) 
	{
		Param<int> cache;
		if (ints.TryGetValue (id, out cache))
		{
			if (cache != value)
			{
				Animator.SetInteger (cache.hash, value);
				cache.value = value;
			}
		}
		else Debug.LogError ("Can't find parameter!", this);
	}

	public void SetTrigger (string id, bool reset=false)  
	{
		int hash;
		if (triggers.TryGetValue(id, out hash))
		{
			if (reset) Animator.ResetTrigger (hash);
			else Animator.SetTrigger (hash);
		}
		else Debug.LogError ("Can't find parameter!", this);
	}
	#endregion
}