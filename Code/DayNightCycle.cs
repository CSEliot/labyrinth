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
// Drives a looping day/night cycle from a single 0..1 Degree value: the light follows
// an arc that peaks at _ElevationDegrees, the moon object orbits opposite the sun, and
// at night the light takes the moon's angle (hard day/night switch for now).
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
    [Property, Range( 0f, 90f )] private float _ElevationDegrees { get; set; } = 40f; // PEAK altitude of the light arc in degrees (noon/midnight)
    [Property] private SkyBox2D _Skybox { get; set; }
    [Property] private DirectionalLight _Sun { get; set; }
    // The moon object. It orbits the world origin opposite the sun at the distance it was authored at.
    [Property] private GameObject _Moon { get; set; }
    [Property] private Color _DayColor { get; set; }
    [Property] private Color _NightColor { get; set; }

    // Moon distance from the world origin, captured from its authored position the first time Apply
    // runs (OnEnabled runs before OnStart, so capture lazily). 0 = not captured yet; a hotload also
    // leaves it 0 and the capture then reads the moon's current (already orbiting) distance.
    private float _moonRadius;

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

        // _Moon is optional: without it the cycle still drives the sky and the light, only the
        // moon object stops orbiting, so it is not part of the missing-reference warning.
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

    // Single place that pushes the current Degree to the material, the light rotation, the moon
    // object and the light colour, so start/enable/update can never disagree.
    private void Apply()
    {
        // The altitude is a cosine of the phase, so it peaks at Degree 0 (noon), drops through
        // the horizon at 0.25 (sunset), bottoms out at 0.5 (midnight) and rises again at 0.75.
        // The light TRAVELS along its Forward (the axis its gizmo arrows are drawn on), so a
        // positive pitch aims Forward DOWN out of the sky, which is the only valid state for the
        // light. The sky shader draws the disc at the negation of that travel, i.e. along
        // Backward, so a light pitched by sunAltitude puts its disc sunAltitude above the horizon.
        // sunRotation itself goes negative at night; it then only places the moon, never the light.
        // Yaw is composed on the LEFT, so it is applied in world space on top of the pitch.
        float phase = Degree * MathF.PI * 2f;                        // 0 = noon
        float arc = MathF.Cos( phase );                              // 1 noon, 0 at the horizon, -1 midnight
        float sunAltitude = _ElevationDegrees * arc;                 // degrees above the horizon
        // Night is tested on the cosine, not the altitude, so a peak of 0 degrees still has nights.
        bool isNight = arc < 0f;
        float yaw = _SweepDegrees * Degree;
        Rotation sunRotation = Rotation.FromYaw( yaw ) * Rotation.From( sunAltitude, 0f, 0f );

        // Below the horizon is night. Hard switch for now (Task 1 turns this into a fade); this
        // is the ONE place the night test lives.
        if ( _SkyBoxMaterial is not null )
            _SkyBoxMaterial.Set( "g_flNightBlend", isNight ? 1f : 0f );

        ApplyMoon( sunRotation );

        if ( _Sun is null )
            return;

        // This component owns the light's rotation. At night the one light takes the moon's
        // angle: the moon sits opposite the sun, so its disc (the light's Backward) is drawn on
        // the moon object instead of on the sun, which is below the horizon now.
        _Sun.WorldRotation = isNight
            ? Rotation.FromYaw( yaw + 180f ) * Rotation.From( -sunAltitude, 0f, 0f )   // disc on the moon
            : sunRotation;

        // Never pure black: a black directional light means "sun off" and the sky shader
        // stops drawing the sun/moon disc. Day and night are two lit tints, swapped whole.
        _Sun.LightColor = isNight ? _NightColor : _DayColor;
    }

    // Keeps the moon object opposite the sun at its authored distance, turning with the orbit so
    // its local +X still faces the world origin (its child light keeps the far-side offset).
    private void ApplyMoon( Rotation sunRotation )
    {
        if ( !_Moon.IsValid() ) return;                // unset or destroyed
        if ( _moonRadius <= 0f ) _moonRadius = _Moon.WorldPosition.Length;
        if ( _moonRadius <= 0f ) return;              // authored at the origin: nothing to orbit
        _Moon.WorldPosition = sunRotation.Forward * _moonRadius;   // opposite the sun
        _Moon.WorldRotation = sunRotation * Rotation.FromYaw( 180f ); // local +X faces the origin
    }

    private string MissingReferenceName()
    {
        if ( _Skybox is null ) return "_Skybox";
        if ( _Sun is null ) return "_Sun";
        return "sky material copy";
    }
}
