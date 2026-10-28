// Project: labyrinth
// File:    MazeGameCC.cs
// Author:  cseliot
// Created: 2026.10.28.10.10.12
// Edited: 2026.10.28.10.22.12
// 
// Copyright (c) 2026 KiteLion Games, LLC. All rights reserved.
// 
// This source code is the property of KiteLion Games, LLC and may not be
// copied, distributed, modified, or used in any way without prior written
// permission from KiteLion Games, LLC.
// 
// Description:
// [Provide a brief description of what this file/class does.]
// 
// License:
// This code is provided "as is," without warranty of any kind, express or
// implied, including but not limited to the warranties of merchantability,
// fitness for a particular purpose, and noninfringement. In no event shall
// the authors or copyright holders be liable for any claim, damages, or
// other liability, whether in an action of contract, tort, or otherwise,
// arising from, out of, or in connection with the software or the use or
// other dealings in the software.

namespace Sandbox;

public sealed class MazeGameCC : Component
{

	private GameManager _gameManager;
	private bool _handled = false;
	
	public void Handle( GameManager gameManager )
	{
		_gameManager = gameManager;
		_handled = true;
	}
	
	protected override void OnStart()
	{
		if ( IsProxy == false )
		{
			var gameManager = Scene.FindAllWithTag( "gamemanager" ).First();
			if(gameManager != null)
				gameManager.GetComponent<GameManager>().LocalCCSayHello( this );
			else
			{
				Log.Info( "No gamemanager tag found! Tag issue?" );
			}
		}
	}

	protected override void OnUpdate()
	{
		if( _handled == false )
			Log.Info("Waiting to be handled ...");
	}
}

