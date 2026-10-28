// Project: labyrinth
// File:    MazeGenerator.cs
// Author:  cseliot
// Created: 2026.10.28.08.10.05
// Edited: 2026.10.28.08.53.05
// 
// Copyright (c) 2026 KiteLion Games, LLC. All rights reserved.
// 
// This source code is the property of KiteLion Games, LLC and may not be
// copied, distributed, modified, or used in any way without prior written
// permission from KiteLion Games, LLC.
// 
// Description:
// Maze creation logic
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

namespace Sandbox;

public sealed class MazeGenerator : Component
{
	[Property] private GameObject _Oakblock  { get; set; }
    // ReSharper disable once UnusedMember.Local
    [Property] private GameObject _GrassBlock { get; set; }
    [Property] private GameObject _BrickBlock { get; set; }
    [Property] private GameObject _GlassBlock { get; set; }

    private struct HelperCoordes( int _X, int _Y )
    {
        public readonly int X = _X;
        public readonly int Y = _Y;
    }
    
    //The walls of our labyrinth
    [Property] private int _MazeHeight { get; set; } = 10;
    [Property] private int _MazeWidth  { get; set; } = 10;
    [Property] private int _BlockLength { get; set; } = 300;
    [Property] private int _BlockWidth  { get; set; } = 300;
    [Property] private int _BlockHeight  { get; set; } = 600;
    [Property] private int _Levels { get; set; } = 1;
    private static int[,] _mainMaze;
    private static int[,] _windows;

    //Let the procedural magic begin
    protected override void OnStart()
    {
        _mainMaze = new int[_MazeWidth, _MazeHeight];
        _windows = new int[_MazeWidth, _MazeHeight];
        
        for (int k = 0; k < _Levels; k++)
        {
            //We create a maze with walls by default
            HelperMazeCreate();
            //We generate rooms
            HelperGenerateRooms();
            //Turning the walls into Bricks and windows
            HelperMakeWindows(5);

            //Let's draw!
            for (int i = 1; i < _MazeWidth - 1; i++)
                for (int j = 1; j < _MazeHeight - 1; j++)
                {
                    //Walls in height 3 blocks
                    if (HelperGetBlockByNumber(_mainMaze[i, j]) != null)
                    {
                        HelperGetBlockByNumber(_mainMaze[i, j]).Clone(new Vector3(i * _BlockLength, j*_BlockHeight, k * _BlockWidth * 4 + 1*_BlockWidth), Rotation.Identity);
                        HelperGetBlockByNumber(_mainMaze[i, j]).Clone(new Vector3(i * _BlockLength, j*_BlockHeight, k * _BlockWidth * 4 + 2*_BlockWidth), Rotation.Identity);
                        HelperGetBlockByNumber(_mainMaze[i, j]).Clone(new Vector3(i * _BlockLength, j*_BlockHeight, k * _BlockWidth * 4 + 3*_BlockWidth), Rotation.Identity);
                    }
                    //Ceiling and floor
                    _Oakblock.Clone(  new Vector3(i * _BlockLength, j, k * 4 * _BlockHeight), Rotation.Identity);
                    _BrickBlock.Clone(new Vector3(i * _BlockLength, j, k * 4 * _BlockHeight + 4 * _BlockHeight), Rotation.Identity);
                }
            //Walls/Windows
            for (int i = 0; i < _MazeWidth; i++)
            {
                for (int j = 0; j < _MazeHeight; j++)
                {
                    if (HelperGetBlockByNumber(_windows[i, j]) != null)
                    {
                        //Floor surround of the building
                        _BrickBlock.Clone(new Vector3(i * _BlockLength, j, _BlockHeight * k * 4), Rotation.Identity);
                        _BrickBlock.Clone(new Vector3(i * _BlockLength, j, _BlockHeight * k * 4 + 1 *_BlockHeight), Rotation.Identity);
                        //Windows
                        HelperGetBlockByNumber(_windows[i, j]).Clone(new Vector3(i * _BlockLength, j * _BlockWidth, _BlockHeight * k * 4 + 2 * _BlockHeight), Rotation.Identity);
                        HelperGetBlockByNumber(_windows[i, j]).Clone(new Vector3(i * _BlockLength, j * _BlockWidth, _BlockHeight * k * 4 + 3 * _BlockHeight), Rotation.Identity);
                    }
                }
            }

        }
    }

    private static bool HelperTossCoin()
    {
        return Game.Random.Next(0, 2) == 1;
    }
    
    //Generating confined rooms
    //roomSquareCount - algorithm iterations
    private void HelperGenerateRooms(int roomSquareCount = 5)
    {
        if (_MazeWidth / roomSquareCount < 2 || _MazeHeight / roomSquareCount < 2)
        {
            throw new Exception("Dude, you can get obsessed with the problem, you can't generate that many walls.");
        }
        int squareCount = 0;

        while (squareCount < roomSquareCount)
        {
            //We check the horizontal
            bool isHorizontal = HelperTossCoin();
            //We generate a random point on the wall
            int pivotPoint = Game.Random.Next(2, isHorizontal ? _MazeWidth - 2 : _MazeHeight - 2);
            //It is important that the line is drawn, otherwise we will redraw it
            if (HelperDrawLine(pivotPoint, isHorizontal))
                squareCount++;
        }
    }

    //The function draws a line in ones (isHorizontal - horizontal, if false - vertical)
    //The units are driving until they hit)
    private bool HelperDrawLine(int pivot, bool isHorizontal = true)
    {
        //Array of wall coordinates to select a random one from them and make a door
        List<HelperCoordes> wallCoordsList = new List<HelperCoordes>();

        //We draw exactly what line
        if (isHorizontal)
        {
            //Of the two faces of the square, we will choose in which direction to lay
            if (HelperTossCoin())
            {
                //Don't get to where the line was already drawn, yes
                //and if there are walls nearby, send such generation to hell
                if (_mainMaze[1, pivot] == 1 || _mainMaze[1, pivot + 1] == 1 || _mainMaze[1, pivot - 1] == 1) return false;
                //An unpleasant cycle has the following meaning: We build a wall before we crash into another
                //Along the way, filling in the coordinates of the constructed wall to create a door at the end of the function
                for (int i = 1; i < _MazeWidth; i++)
                {
                    //If we hit a wall/door, then we go out
                    if (_mainMaze[i, pivot] > 0) break;
                    _mainMaze[i, pivot] = 1;
                    wallCoordsList.Add(new HelperCoordes(i, pivot));
                }
            }
            else
            {
                //Don't get to where the line was already drawn
                if (_mainMaze[_MazeWidth - 1, pivot] == 1 || _mainMaze[_MazeWidth - 1, pivot + 1] == 1 || _mainMaze[_MazeWidth - 1, pivot - 1] == 1) return false;
                //Reverse:)))
                for (int i = _MazeWidth - 1; i >= 1; i--)
                {
                    if (_mainMaze[i, pivot] > 0) break;
                    _mainMaze[i, pivot] = 1;
                    wallCoordsList.Add(new HelperCoordes(i, pivot));
                }
            }
        }
        else
        //Same code as above, vertical only
        {
            if (HelperTossCoin())
            {
                if (_mainMaze[pivot, 1] == 1 || _mainMaze[pivot + 1, 1] == 1 || _mainMaze[pivot - 1, 1] == 1) return false;
                for (int i = 1; i < _MazeHeight; i++)
                {
                    if (_mainMaze[pivot, i] > 0) break;
                    wallCoordsList.Add(new HelperCoordes(pivot, i));
                    _mainMaze[pivot, i] = 1;
                }
            }
            else
            {
                if (_mainMaze[pivot, _MazeHeight - 1] == 1 || _mainMaze[pivot + 1, _MazeHeight - 1] == 1 || _mainMaze[pivot - 1, _MazeHeight - 1] == 1) return false;
                for (int i = _MazeHeight - 1; i >= 1; i--)
                {
                    if (_mainMaze[pivot, i] > 0) break;
                    _mainMaze[pivot, i] = 1;
                    wallCoordsList.Add(new HelperCoordes(pivot, i));
                }
            }
        }
        //We knock down the door! like a Captain Price Style
        if (wallCoordsList.Count > 0)
        {
            //We don’t knock it out right away. We check - what if the door leads into the wall (T-shaped intersection)
            HelperCoordes door;
            bool tcriteria;
            do
            {
                door = wallCoordsList[Game.Random.Next(0, wallCoordsList.Count - 1)];
                //Yes, this is a condition for the T-case. Either 111 passes perpendicularly through 000 or vice versa, there is no third option
                if (isHorizontal)
                    tcriteria = !(_mainMaze[door.X, door.Y + 1] == 0 || _mainMaze[door.X, door.Y - 1] == 0);
                else
                    tcriteria = !(_mainMaze[door.X + 1, door.Y] == 0 || _mainMaze[door.X - 1, door.Y] == 0);
            } while (tcriteria);
            _mainMaze[door.X, door.Y] = 2;
        }
        return true;
    }

    //Generation of a hollow huge room with cleaning
    private void HelperMazeCreate()
    {
        if (_mainMaze == null)
            _mainMaze = new int[_MazeWidth, _MazeHeight];

        for (int i = 0; i < _MazeWidth; i++)
            for (int j = 0; j < _MazeHeight; j++)
                if (i == 0 || j == 0 || i == _MazeWidth - 1 || j == _MazeHeight - 1)
                    _mainMaze[i, j] = 1;
                else
                    _mainMaze[i, j] = 0;
    }

    private void HelperMakeWindows(int windowMaxWidth = 2)
    {
        //Our assistant is the counter
        int counter = 0;
        //Upper face
        for (int i = 0; i < _MazeHeight; i++)
        {
            if (counter < windowMaxWidth)
            {
                //We checked - if there is a window with a wall, well, it - we move on
                if (_mainMaze[1, i] == 1)
                {
                    //The brick was laid
                    _windows[0, i] = 3;
                    //The next iteration of the loop right away
                    continue;
                }
                //If ok, we put it
                _windows[0, i] = 4;
                counter++;
            }
            else
            {
                //The brick was laid
                _windows[0, i] = 3;
                //We rested at intervals
                counter = 0;
            }
        }
        counter = 0;
        //Left face
        for (int i = 0; i < _MazeWidth; i++)
        {
            if (counter < windowMaxWidth)
            {
                //We checked - if there is a window with a wall, well, it - we move on
                if (_mainMaze[i, 1] == 1)
                {
                    //The brick was laid
                    _windows[i, 0] = 3;
                    //The next iteration of the loop right away
                    continue;
                }
                //If ok, we put it
                _windows[i, 0] = 4;
                counter++;
            }
            else
            {
                //The brick was laid
                _windows[i, 0] = 3;
                //We rested at intervals
                counter = 0;
            }
        }
        //Right face
        for (int i = 0; i < _MazeWidth; i++)
        {
            if (counter < windowMaxWidth)
            {
                //We checked - if there is a window with a wall, well, it - we move on
                if (_mainMaze[i, _MazeHeight - 2] == 1)
                {
                    //The brick was laid
                    _windows[i, _MazeHeight - 1] = 3;
                    //The next iteration of the loop right away
                    continue;
                }
                //If ok, we put it
                _windows[i, _MazeHeight - 1] = 4;
                counter++;
            }
            else
            {
                //The brick was laid
                _windows[i, _MazeHeight - 1] = 3;
                //We rested at intervals
                counter = 0;
            }
        }
        //The bottom face
        for (int i = 0; i < _MazeWidth; i++)
        {
            if (counter < windowMaxWidth)
            {
                //We checked - if there is a window with a wall, well, it - we move on
                if (_mainMaze[_MazeWidth - 2, i] == 1)
                {
                    //The brick was laid
                    _windows[_MazeWidth - 1, i] = 3;
                    //The next iteration of the loop right away
                    continue;
                }
                //If ok, we put it
                _windows[_MazeWidth - 1, i] = 4;
                counter++;
            }
            else
            {
                //The brick was laid
                _windows[_MazeWidth - 1, i] = 3;
                //We rested at intervals
                counter = 0;
            }
        }
    }

    GameObject HelperGetBlockByNumber(int c)
    {
        switch (c)
        {
            //Replace with characters 
            case 1: return _Oakblock;
            case 2: 
                Log.Info("WANTED GRASS");
                return null;
            case 3: return _BrickBlock;
            case 4: return _GlassBlock;
            //case 'S': return spring; break;
            default: return null;
        }
    }
}

