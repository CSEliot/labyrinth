// Project: labyrinth
// File:    DayNightCycle.cs
// Author:  cseliot
// Created: 2026.10.28.17.10.58
// Edited: 2026.10.28.22.13.25
// 
// Copyright (c) 2026 KiteLion Games, LLC. All rights reserved.
// 
// This source code is the property of KiteLion Games, LLC and may not be
// copied, distributed, modified, or used in any way without prior written
// permission from KiteLion Games, LLC.
// 
// Description:
// Drives a looping day/night cycle from a single 0..1 Degree value: blends the
// SkyBox2D night material parameter, tints the directional light, and yaws the
// light so the sun/moon disc travels across the sky.
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

public sealed class DayNightCycle : Component
{

    [Property] public float Degree {
        get => _degree;
        set => _degree = value.UnsignedMod(1f);
    }

    private float _degree = 0.5f;

    // Wall-clock length of one full cycle in seconds. Time.Delta / _CycleSeconds advances
    // Degree per frame, so the cycle is real time and independent of frame rate.
    [Property, Range( 10f, 3600f )] private float _CycleSeconds { get; set; } = 180f;
    [Property] private float _SweepDegrees { get; set; } = 360f; // world-space yaw over a full cycle; 360 keeps the wrap seamless
    [Property, Range( 0f, 90f )] private float _ElevationDegrees { get; set; } = 40f; // pitch of the light above the horizon
    [Property] private SkyBox2D _Skybox { get; set; }
    [Property] private DirectionalLight _Sun { get; set; }
    [Property] private Color _DayColor { get; set; }
    [Property] private Color _NightColor { get; set; }

    // Private copy of the sky material: Material.Set on the shared loaded .vmat flags the
    // asset as edited (dirty in the editor every tick), so we only ever write to this copy.
    private Material _SkyBoxMaterial;

    protected override void OnStart()
    {
        // Write to a private copy, and hand that copy back to the skybox: Set() on the shared
        // loaded .vmat marks the asset edited, but a copy the skybox never renders would show
        // nothing at all.
        if ( _Skybox is not null && _Skybox.SkyMaterial is not null )
        {
            var source = _Skybox.SkyMaterial;
            var copy = source.CreateCopy();

            // SkyBox2D only accepts materials whose shader name contains "sky". A procedural
            // copy may not report one, so fall back to the shared material in that case: it
            // dirties the .vmat, but the blend still works.
            if ( copy is not null && copy.ShaderName.Contains( "sky" ) )
            {
                _SkyBoxMaterial = copy;
                _Skybox.SkyMaterial = copy;
            }
            else
            {
                _SkyBoxMaterial = source;
            }
        }

        if ( _Skybox is null || _Sun is null || _SkyBoxMaterial is null )
            Log.Warning( $"DayNightCycle on '{GameObject.Name}': missing {MissingReferenceName()} - the day/night cycle will not render." );

        // Apply immediately so the sky never renders one tick of the .vmat default (full night)
        // before Degree is pushed.
        Apply();
    }

    protected override void OnEnabled()
    {
        Apply();
    }

    protected override void OnUpdate()
    {
        Degree += Time.Delta / _CycleSeconds;
        Apply();
    }

    // Single place that pushes the current Degree to the material, the light rotation and the
    // light colour, so start/enable/update can never disagree.
    private void Apply()
    {
        if ( _SkyBoxMaterial is not null )
            _SkyBoxMaterial.Set( "g_flNightBlend", Degree );

        if ( _Sun is null )
            return;

        // This component owns the light's rotation: the authored pitch in the scene is replaced
        // by _ElevationDegrees, so the disc is always above the horizon.
        // The light TRAVELS along its Forward (the axis its gizmo arrows are drawn on), so a
        // positive pitch aims Forward DOWN out of the sky, which is the only valid state for a
        // sun or moon. The sky shader draws the disc at the negation of that travel, i.e. along
        // Backward, so the disc ends up _ElevationDegrees above the horizon.
        // Yaw is composed on the LEFT, so it is applied in world space on top of the pitch.
        _Sun.WorldRotation = Rotation.FromYaw( _SweepDegrees * Degree ) * Rotation.From( _ElevationDegrees, 0f, 0f );

        // Never pure black: a black directional light means "sun off" and the sky shader
        // stops drawing the sun/moon disc. Lerping between two lit colours stays non-black.
        _Sun.LightColor = Color.Lerp(_DayColor, _NightColor, Degree);
    }

    private string MissingReferenceName()
    {
        if ( _Skybox is null ) return "_Skybox";
        if ( _Sun is null ) return "_Sun";
        return "sky material copy";
    }
}
