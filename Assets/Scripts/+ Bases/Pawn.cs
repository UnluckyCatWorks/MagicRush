﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pawn : MonoBehaviour
{
	#region DATA
	public Player Owner 
	{
		get
		{
			Player owner;
			// This allows to play with characters without controls
			// configured, or just to easily change them during gameplay
			if (overrideControl != ControllerType.UNESPECIFIED) 
			{
				if (_override == null) _override = new Player (overrideControl);
				else _override.scheme.type = overrideControl;

				owner = _override;
			}
			else
			{
				// -1 so that 0 => NULL
				int id = ownerID - 1;

				// If not overriding, find owner player
				if (id == -1) owner = null;
				else owner = Player.all[id];
			}
			return owner;
		}
	}
	private Player _override;

	[Header ("Pawn settings")]
	public int ownerID;
	public ControllerType overrideControl;
	#endregion
}