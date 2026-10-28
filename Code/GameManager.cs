// Project: labyrinth
// File:    GameManager.cs
// Author:  cseliot
// Created: 2026.10.28.09.10.35
// Edited: 2026.10.28.09.38.35
// 
// Copyright (c) 2026 KiteLion Games, LLC. All rights reserved.
// 
// This source code is the property of KiteLion Games, LLC and may not be
// copied, distributed, modified, or used in any way without prior written
// permission from KiteLion Games, LLC.
// 
// Description:
// Root state holder and global values manager.
// 
// License:
// This code is provided "as is," without warranty of any kind, express or
// implied, including but not limited to the warranties of merchantability,
// fitness for a particular purpose, and noninfringement. In no event shall
// the authors or copyright holders be liable for any claim, damages, or
// other liability, whether in an action of contract, tort, or otherwise,
// arising from, out of, or in connection with the software or the use or
// other dealings in the software.

using System;
using Sandbox.Network;

namespace Sandbox;

public sealed class GameManager : Component
{

    [Property] public int Seed { get; set; } = 1;

    /// <summary>
	/// Order matters here!
	/// </summary>
	[Property] private List<GameObject> _GameSingletons { get; set; } = new ();
	[Property] private NetworkHelper _NetworkHelper { get; set; }

	private bool _nonOrderedDependenciesReady = false;
	private MazeGameCC _localMazeGameCC;
	private bool _nonOrderedDependenciesHandled = false;

	public void LocalCCSayHello( MazeGameCC mazeGameCC )
	{
		_localMazeGameCC =  mazeGameCC;
	}
	
	protected override void OnStart()
	{
		HelperDoOrderedSetup();
	}

	protected override void OnUpdate()
	{
		if(_localMazeGameCC != null)
			_nonOrderedDependenciesReady = true;

		if ( _nonOrderedDependenciesReady && _nonOrderedDependenciesHandled == false )
			HelperDoNonOrderedSetup();


	}

	private void HelperDoNonOrderedSetup()
	{
		_localMazeGameCC.Handle(this);
		_nonOrderedDependenciesReady = true;
	}

	/// <summary>
	/// First stage of game initialization, second is HelperDoNonOrderedSetup().
	/// </summary>
	private void HelperDoOrderedSetup()
	{
		List<string> startErrors = new();
		if(_NetworkHelper == null)
			startErrors.Add("NetworkHelper not set");

		if ( startErrors.Count > 0 )
		{
			Log.Error(startErrors);
			return;
		}
		
		Game.SetRandomSeed( Seed );
		for ( int i = 0; i < _GameSingletons.Count; i++ )
		{
			var go = _GameSingletons[i];
			var g = go.Clone();
			g.Parent = GameObject;
		}
		
		_NetworkHelper?.StartServer = true;
		Networking.CreateLobby(new LobbyConfig());
	}
}

