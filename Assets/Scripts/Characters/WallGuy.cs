﻿using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WallGuy : Character
{
	[Header ("Spell Settings")]
	public GameObject wall;
	public float wallDuration;
	public float wallDistance;
	public float maxWallDistance;

	protected override void CastSpell () 
	{
		// Get all 'grounds'
		var grounds = Physics.OverlapSphere ( Vector3.zero, 30f, 1<<9 );

		// Iterate through and find closest available location to casting point
		var castPoint = transform.position + movingDir*wallDistance;
		Vector3 closestCastPoint = Vector3.zero;
		float closestDistance = 100f;
		foreach (var c in grounds)
		{
			var closestPoint = c.ClosestPoint (castPoint);
			var dist = Vector3.Distance (castPoint, closestPoint);
			if (dist <= maxWallDistance && dist < closestDistance)
			{
				closestCastPoint = closestPoint;
				closestDistance = dist;
			}
		}

		if (closestCastPoint != Vector3.zero)
		{
			// Build wall
			var go = Instantiate (wall);
			go.transform.position = closestCastPoint;
			go.transform.rotation = transform.rotation;
		}
		else print ("las paredes van regu eh");
	}
}