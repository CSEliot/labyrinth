HEADER
{
	DevShader = true;
	Description = "Night Sky (moon + stars, dimmed atmosphere)";
	Version = 1;
}

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
MODES
{
	Forward();
}

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
FEATURES
{
	#include "vr_common_features.fxc"
}

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
COMMON
{
	#include "system.fxc"
	#include "vr_common.fxc"

	static const float kAtmosPi = 3.141592f;
	static const int kAtmosPrimarySteps = 16;
	static const int kAtmosSecondarySteps = 2;

	// ---- Tunables (all exposed to the material editor via Default*/Range/UiGroup) ---------------------------------------------------------------
	float g_flNightBlend        < Default( 1.0 );       Range( 0.0, 1.0 );      UiGroup( "Cycle,05/10" ); >;  // 0 = day (stock atmosphere look), 1 = night
	float g_flDayAtmosphere     < Default( 1.0 );       Range( 0.0, 4.0 );      UiGroup( "Cycle,05/20" ); >;  // day scattering multiplier
	float g_flNightAtmosphere   < Default( 0.03 );      Range( 0.0, 1.0 );      UiGroup( "Night Sky,10/10" ); >;  // how much of the day scattering survives
	float3 g_vNightZenith       < UiType( Color );      Default3( 0.010, 0.014, 0.035 ); UiGroup( "Night Sky,10/20" ); >;  // sky colour overhead
	float3 g_vNightHorizon      < UiType( Color );      Default3( 0.045, 0.055, 0.090 ); UiGroup( "Night Sky,10/30" ); >;  // sky colour at the horizon

	float g_flStarDensity       < Default( 220.0 );     Range( 20.0, 800.0 );   UiGroup( "Stars,20/10" ); >;  // cells across the sky; higher = more, smaller stars
	float g_flStarBrightness    < Default( 1.35 );      Range( 0.0, 10.0 );     UiGroup( "Stars,20/20" ); >;
	float g_flStarThreshold     < Default( 0.82 );      Range( 0.0, 1.0 );      UiGroup( "Stars,20/30" ); >;  // fraction of cells left empty
	float g_flStarSize          < Default( 0.30 );      Range( 0.02, 1.0 );     UiGroup( "Stars,20/40" ); >;  // point radius, in cell units
	float g_flStarHorizonFade   < Default( 4.0 );       Range( 0.0, 40.0 );     UiGroup( "Stars,20/50" ); >;
	float g_flStarTwinkle       < Default( 0.0 );       Range( 0.0, 1.0 );      UiGroup( "Stars,20/60" ); >;  // 0 = static field

	float g_flMoonSizeDeg       < Default( 2.2 );       Range( 0.2, 20.0 );     UiGroup( "Moon,30/10" ); >;  // angular diameter, degrees
	float3 g_vMoonColor         < UiType( Color );      Default3( 1.0, 0.97, 0.92 ); UiGroup( "Moon,30/15" ); >;  // the moon's own albedo (not the light colour)
	float g_flMoonBrightness    < Default( 3.0 );       Range( 0.0, 50.0 );     UiGroup( "Moon,30/20" ); >;
	float g_flMoonHaloStrength  < Default( 0.35 );      Range( 0.0, 4.0 );      UiGroup( "Moon,30/30" ); >;
	float g_flMoonHaloFalloff   < Default( 12.0 );      Range( 1.0, 128.0 );    UiGroup( "Moon,30/40" ); >;
	float g_flMoonMottle        < Default( 0.25 );      Range( 0.0, 1.0 );      UiGroup( "Moon,30/50" ); >;  // surface variation, hides the flat disc
	float g_flMoonEdgeSoftness  < Default( 0.08 );      Range( 0.0, 1.0 );      UiGroup( "Moon,30/60" ); >;  // fraction of radius used for the soft edge
	float g_flMoonHorizonFade   < Default( 4.0 );       Range( 0.25, 40.0 );    UiGroup( "Moon,30/70" ); >;  // how fast the whole disc fades out as the moon sets

	float g_flSunBrightness     < Default( 10.0 );      Range( 0.0, 200.0 );    UiGroup( "Sun,40/10" ); >;  // stock shader multiplies the disc by 10
	float g_flSunSharpness      < Default( 10000.0 );   Range( 100.0, 200000.0 ); UiGroup( "Sun,40/20" ); >;  // larger = smaller, harder disc

	float2 RaySphereIntersection( float3 origin, float3 direction, float radius )
	{
		float a = dot( direction, direction );
		float b = 2.0f * dot( direction, origin );
		float c = dot( origin, origin ) - ( radius * radius );
		float discriminant = ( b * b ) - 4.0f * a * c;

		if ( discriminant < 0.0f )
		{
			return float2( 1e5f, -1e5f );
		}

		float sqrtDiscriminant = sqrt( discriminant );
		float invDenominator = 0.5f / a;
		return float2(
			( -b - sqrtDiscriminant ) * invDenominator,
			( -b + sqrtDiscriminant ) * invDenominator
		);
	}

	float3 atmosphere( float3 viewDir, float3 rayOrigin, float3 sunDirection, float sunIntensity, float planetRadius, float atmosphereRadius, float3 rayleighCoefficients, float mieCoefficient, float rayleighScaleHeight, float mieScaleHeight, float miePreferredScatteringDirection )
	{
		sunDirection = normalize( sunDirection );
		viewDir = normalize( viewDir );

		float2 intersections = RaySphereIntersection( rayOrigin, viewDir, atmosphereRadius );
		if ( intersections.x > intersections.y )
		{
			return 0.0f;
		}

		intersections.y = min( intersections.y, RaySphereIntersection( rayOrigin, viewDir, planetRadius ).x );
		float primaryStepSize = ( intersections.y - intersections.x ) / float( kAtmosPrimarySteps );

		float rayleighOpticalDepth = 0.0f;
		float mieOpticalDepth = 0.0f;
		float3 accumulatedRayleigh = 0.0f;
		float3 accumulatedMie = 0.0f;

		float cosTheta = dot( viewDir, sunDirection );
		float cosThetaSquared = cosTheta * cosTheta;
		float g = miePreferredScatteringDirection;
		float gSquared = g * g;
		float rayleighPhase = ( 3.0f / ( 16.0f * kAtmosPi ) ) * ( 1.0f + cosThetaSquared );
		float miePhaseDenominator = pow( 1.0f + gSquared - 2.0f * cosTheta * g, 1.5f ) * ( 2.0f + gSquared );
		float miePhaseNumerator = ( 1.0f - gSquared ) * ( cosThetaSquared + 1.0f );
		float miePhase = ( 3.0f / ( 8.0f * kAtmosPi ) ) * ( miePhaseNumerator / miePhaseDenominator );

		float primaryTime = 0.0f;

		[loop]
		for ( int primaryStep = 0; primaryStep < kAtmosPrimarySteps; ++primaryStep )
		{
			float3 samplePosition = rayOrigin + viewDir * ( primaryTime + primaryStepSize * 0.5f );
			float sampleHeight = length( samplePosition ) - planetRadius;

			float rayleighStep = exp( -sampleHeight / rayleighScaleHeight ) * primaryStepSize;
			float mieStep = exp( -sampleHeight / mieScaleHeight ) * primaryStepSize;

			rayleighOpticalDepth += rayleighStep;
			mieOpticalDepth += mieStep;

			float secondaryStepSize = RaySphereIntersection( samplePosition, sunDirection, atmosphereRadius ).y / float( kAtmosSecondarySteps );
			float secondaryTime = 0.0f;
			float secondaryRayleighOpticalDepth = 0.0f;
			float secondaryMieOpticalDepth = 0.0f;

			[loop]
			for ( int secondaryStep = 0; secondaryStep < kAtmosSecondarySteps; ++secondaryStep )
			{
				float3 secondaryPosition = samplePosition + sunDirection * ( secondaryTime + secondaryStepSize * 0.5f );
				float secondaryHeight = length( secondaryPosition ) - planetRadius;

				secondaryRayleighOpticalDepth += exp( -secondaryHeight / rayleighScaleHeight ) * secondaryStepSize;
				secondaryMieOpticalDepth += exp( -secondaryHeight / mieScaleHeight ) * secondaryStepSize;

				secondaryTime += secondaryStepSize;
			}

			float3 transmittance = exp( -( mieCoefficient * ( mieOpticalDepth + secondaryMieOpticalDepth ) + rayleighCoefficients * ( rayleighOpticalDepth + secondaryRayleighOpticalDepth ) ) );

			accumulatedRayleigh += rayleighStep * transmittance;
			accumulatedMie += mieStep * transmittance;

			primaryTime += primaryStepSize;
		}

		float3 rayleighContribution = rayleighPhase * rayleighCoefficients * accumulatedRayleigh;
		float3 mieContribution = ( miePhase * mieCoefficient ) * accumulatedMie;

		return sunIntensity * ( rayleighContribution + mieContribution );
	}
}

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
struct VS_INPUT
{
	float4 vPositionOs : POSITION < Semantic( PosXyz ); >;
};

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
struct PS_INPUT
{
	float3 vPositionWs : TEXCOORD1;

	#if ( PROGRAM == VFX_PROGRAM_VS )
		float4 vPositionPs	: SV_Position;
	#endif
	#if ( PROGRAM == VFX_PROGRAM_PS )
		float4 vPositionSs : SV_Position;
	#endif
};

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
VS
{
	// Includes -----------------------------------------------------------------------------------------------------------------------------------------------
	#define IS_SPRITECARD 1
	#include "system.fxc"

	// Main ---------------------------------------------------------------------------------------------------------------------------------------------------
	PS_INPUT MainVs( const VS_INPUT i )
	{
		PS_INPUT o;
		float flSkyboxScale = g_flNearPlane + g_flFarPlane;
		float3 vPositionWs = g_vCameraPositionWs.xyz + i.vPositionOs.xyz * flSkyboxScale;

		o.vPositionPs = Position3WsToPs( vPositionWs );
		o.vPositionWs = vPositionWs;

		return o;
	}
}

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
PS
{
	#include "vr_lighting.fxc"
	#include "volumetric_fog.fxc"
	#include "common/Shadow.hlsl"

	// Render State -------------------------------------------------------------------------------------------------------------------------------------------
	RenderState( CullMode, NONE );
	RenderState( DepthWriteEnable, false );
	RenderState( DepthEnable, true );
	RenderState( DepthFunc, GREATER_EQUAL );

	// Attributes ---------------------------------------------------------------------------------------------------------------------------------------------
	BoolAttribute( sky, true );

	// Output -------------------------------------------------------------------------------------------------------------------------------------------------
	struct PS_OUTPUT
	{
		float4 vColor0 : SV_Target0;
	};

	// Helpers ------------------------------------------------------------------------------------------------------------------------------------------------

	float noise( in float3 x )
	{
		float3 p = floor(x);
		float3 f = frac(x);
		f = f*f*(3.0-2.0*f);
		float2 uv = (p.xy+float2(37.0,17.0) * p.z) + f.xy;
		float2 rg = g_tBlueNoise.Sample( g_sBilinearWrap, (uv + 0.5 )/256.0 ).xy;
		return lerp( rg.x, rg.y, f.z );
	}

	float Hash11( float3 p )
	{
		p = frac( p * 0.1031 );
		p += dot( p, p.yzx + 33.33 );
		return frac( ( p.x + p.y ) * p.z );
	}

	float3 Hash31( float3 p )
	{
		float3 q = float3(
			dot( p, float3( 127.1, 311.7, 74.7 ) ),
			dot( p, float3( 269.5, 183.3, 246.1 ) ),
			dot( p, float3( 113.5, 271.9, 124.6 ) ) );
		return frac( sin( q ) * 43758.5453 );
	}

	// Scattering, same model as the stock atmosphere shader but scaled for night.
	float3 GetAtmosphere( float3 ray, float3 moonDirectionWs, float3 moonColor )
	{
		float fPlanetSize = 6371e3;
		float fAtmosphereSize = 100e3;
		float fSeaLevel = 512.0f;

		float3 color = atmosphere
		(
			ray.xzy,                                                        // normalized ray direction
			float3(0,fPlanetSize + g_vCameraPositionWs.z + fSeaLevel,0),     // ray origin
			moonDirectionWs.xzy,                                            // direction to the light
			50,                                                             // intensity of the light
			fPlanetSize,                                                    // radius of the planet in meters
			fPlanetSize + fAtmosphereSize,                                  // radius of the atmosphere in meters
			float3(5.5e-6, 13.0e-6, 22.4e-6),                               // Rayleigh scattering coefficient
			21e-6,                                                          // Mie scattering coefficient
			8e3,                                                            // Rayleigh scale height
			1.2e3,                                                          // Mie scale height
			0.758                                                           // Mie preferred scattering direction
		);

		return color * moonColor;
	}

	// Point stars: one candidate per 3D cell of the direction vector, sampled over the
	// neighbouring cells so points are not clipped by cell borders. No viewport dependence,
	// so the field is stable as the camera moves.
	float3 StarField( float3 rayDir )
	{
		float3 p = rayDir * g_flStarDensity;
		float3 cell = floor( p );
		float3 accum = 0.0f;

		// Angular size of one pixel, from the screen-space derivative of the view ray. At
		// high density a star is smaller than a pixel, so a fixed radius makes its peak
		// brightness a sub-pixel lottery that shimmers as the camera pans. Growth is capped
		// in cell units and energy-compensated below, so on-screen brightness is preserved.
		float pixAng = max( length( fwidth( rayDir ) ), 1e-6f );
		float minCells = pixAng * g_flStarDensity * 0.75f;

		[loop]
		for ( int x = -1; x <= 1; x++ )
		[loop]
		for ( int y = -1; y <= 1; y++ )
		[loop]
		for ( int z = -1; z <= 1; z++ )
		{
			float3 c = cell + float3( x, y, z );

			// Most cells are empty, so test occupancy before the position hash: Hash31 is
			// three sin calls and would be discarded by the continue below ~82% of the time.
			float exists = step( g_flStarThreshold, Hash11( c + 11.7 ) );
			if ( exists <= 0.0f )
				continue;

			float3 h = Hash31( c );

			float3 starPos = c + h;
			float dist = length( p - starPos );

			// Brighter stars read slightly larger; the sqrt keeps the size range tight.
			float baseRadius = g_flStarSize * ( 0.45f + 0.75f * sqrt( h.y ) );
			float radius = max( baseRadius, minCells );
			float point = 1.0f - saturate( dist / max( radius, 1e-4f ) );
			point = point * point;

			// Spreading the point over a larger radius thins it out, so scale by the area
			// ratio to keep the integrated energy (and total sky brightness) constant.
			float energy = ( baseRadius * baseRadius ) / ( radius * radius );

			// Luminance spread: a few bright ones, many faint.
			float magnitude = pow( saturate( h.y ), 3.0f ) + 0.08f;

			// Colour: mostly white, some blue, some warm.
			float3 tint = lerp( float3( 1.0f, 0.92f, 0.82f ), float3( 0.80f, 0.88f, 1.0f ), saturate( h.z * 1.4f ) );

			float twinkle = 1.0f;
			if ( g_flStarTwinkle > 0.0f )
			{
				twinkle = 1.0f - g_flStarTwinkle * ( 0.5f + 0.5f * sin( g_flTime * 2.3f + h.x * 61.0f ) );
			}

			accum += point * magnitude * twinkle * tint * energy;
		}

		accum *= g_flStarBrightness;

		// Gentle horizon fade (the stock shader uses a very harsh vRay.z * 100).
		accum *= saturate( rayDir.z * g_flStarHorizonFade + 0.15f );

		// The soft fade above only reaches 0.15 at the horizon, so the field would still be
		// drawn down to where 0.15 * fade == 0. Hard-clip it at z = 0 (stars never rise from
		// below the horizon); the fade constant stays the softness knob above it.
		accum *= saturate( rayDir.z );

		return accum;
	}

	// Sun: the stock disc (pow on the dot product), now with exposed brightness/sharpness.
	float3 SunDisc( float3 rayDir, float3 lightDirectionWs, float3 lightColor )
	{
		float sun = pow( saturate( dot( rayDir, lightDirectionWs ) + 0.00025f ), g_flSunSharpness );
		sun *= saturate( rayDir.z );   // fade the sun as it drops below the horizon
		return lightColor * sun * g_flSunBrightness;
	}

	// Moon: soft-edged disc at the directional light direction plus a broad halo.
	// The whole disc fades uniformly with the moon's own altitude (not the view ray's),
	// so it dims and disappears as a body instead of being sliced at the horizon line.
	// Returns the shaded disc value and hands the geometric coverage (1 inside the disc,
	// 0 outside, ramped across the soft edge) back to the caller so the halo can be
	// masked off the disc without borrowing the disc's limb/mottle darkening.
	float MoonDisc( float3 rayDir, float3 moonDirectionWs, out float coverage )
	{
		coverage = 0.0f;

		float moonFade = saturate( moonDirectionWs.z * g_flMoonHorizonFade );
		if ( moonFade <= 0.0f )
			return 0.0f;

		// Safe to sample g_tBlueNoise after this early-out: moonFade derives from a light
		// uniform, so the branch is uniform across the whole draw (the ray-direction test
		// that used to sit here was per-pixel and was not).
		float mottle = noise( rayDir * 90.0f );

		// Dot-space angular test: acos loses too much precision on a disc this small
		// (a 2 degree moon sits at dot ~0.999), so compare cosines directly.
		float moonSize = max( g_flMoonSizeDeg, 0.01f );
		float radius = radians( moonSize * 0.5f );
		float cosOuter = cos( radius );
		float cosInner = cos( radius * saturate( 1.0f - g_flMoonEdgeSoftness ) );

		// Floor the edge width relative to the disc's own ( 1 - cos ) scale. An absolute
		// 1e-6 floor only fits large discs: below ~0.16 degrees it exceeds the whole span
		// and the disc never reaches full opacity (0.29 at the centre for a 0.1 degree moon).
		float edgeWidth = max( cosInner - cosOuter, ( 1.0f - cosOuter ) * 1e-3f );

		float cosAngle = dot( rayDir, moonDirectionWs );
		float discT = saturate( ( cosAngle - cosOuter ) / edgeWidth );
		float shape = discT * discT * ( 3.0f - 2.0f * discT );   // smoothstep( cosOuter, cosInner, cosAngle )

		// Limb darkening: brighter centre, dimmer edge. Over a disc this small
		// angle^2 ~ 2 * ( 1 - cos ), so this reproduces ( 1 - 0.35 * angle / radius )^2
		// (1.0 at the centre, ~0.42 at the limb) without an acos of a near-one dot.
		float limbEdge = saturate( sqrt( saturate( ( 1.0f - cosAngle ) / max( 1.0f - cosOuter, 1e-8f ) ) ) );
		float limb = 1.0f - 0.35f * limbEdge;
		limb *= limb;

		// Surface variation so the disc does not read as a flat circle.
		float surface = lerp( 1.0f, 0.55f + 0.9f * mottle, g_flMoonMottle );

		// Geometric coverage only: scaling it by moonFade would let the halo leak back onto
		// the disc as the moon sets (the halo already carries its own moonFade).
		coverage = shape;

		return shape * limb * surface * moonFade;
	}

	// Broad glow around the moon. discCoverage comes from MoonDisc: because the halo is
	// multiplied by ( 1 - discCoverage ) it is exactly zero over the whole disc (limb and
	// mottle keep their full contrast) and back to full strength right at the limb, so
	// there is no gap; the hand-over runs across the disc's own soft edge, so no hard ring.
	float MoonHalo( float3 rayDir, float3 moonDirectionWs, float discCoverage )
	{
		float moonFade = saturate( moonDirectionWs.z * g_flMoonHorizonFade );
		if ( moonFade <= 0.0f )
			return 0.0f;

		float cosAngle = saturate( dot( rayDir, moonDirectionWs ) );
		float halo = pow( cosAngle, max( g_flMoonHaloFalloff, 1.0f ) ) * g_flMoonHaloStrength;

		halo *= 1.0f - saturate( discCoverage );

		return halo * moonFade;
	}

	// Main ---------------------------------------------------------------------------------------------------------------------------------------------------

	PS_OUTPUT MainPs( PS_INPUT i )
	{
		PS_OUTPUT o;

		float3 vPositionWs = i.vPositionWs;
		float3 vRay = normalize( vPositionWs - g_vCameraPositionWs );

		// g_DirectionalLightDirection is the way the light travels, so negate to get direction to it.
		float3 moonDirectionWs = -g_DirectionalLightDirection.xyz;
		float3 moonColor = g_DirectionalLightColor.rgb;

		// A usable direction is all the moon needs. The engine calls a black light "sun off"
		// and stops writing g_DirectionalLightDirection entirely, so gating the moon on the
		// light being lit and coloured (as the atmosphere and the sun disc must be) would
		// delete it at night.
		float dirValid = dot( moonDirectionWs, moonDirectionWs ) > 1e-8f ? 1.0f : 0.0f;

		float lightValid = 0.0f;
		if ( g_DirectionalLightEnabled )
		{
			if ( dirValid > 0.0f && dot( moonColor, moonColor ) > 0.0f )
				lightValid = 1.0f;
		}

		float nightBlend = saturate( g_flNightBlend );
		float dayBlend = saturate( 1.0f - nightBlend );

		// Base night gradient, horizon slightly warmer/brighter than zenith.
		float up = saturate( vRay.z );
		float3 vColor = lerp( g_vNightHorizon, g_vNightZenith, pow( up, 0.45f ) ) * nightBlend;

		// The moon keeps its own albedo and does not need the light to be lit, so disc and
		// halo are gated on the direction alone - only the light-coloured terms below are not.
		float moonCoverage = 0.0f;
		float disc = 0.0f;
		float halo = 0.0f;
		if ( dirValid > 0.0f )
		{
			disc = MoonDisc( vRay, moonDirectionWs, moonCoverage );
			halo = MoonHalo( vRay, moonDirectionWs, moonCoverage );
		}

		if ( lightValid > 0.0f )
		{
			// One scattering model for both ends: full strength by day, a whisper at night.
			float atmosphereIntensity = lerp( g_flDayAtmosphere, g_flNightAtmosphere, nightBlend );
			vColor += GetAtmosphere( vRay, moonDirectionWs, moonColor ) * atmosphereIntensity;

			vColor += SunDisc( vRay, moonDirectionWs, moonColor ) * dayBlend;
		}

		// The moon has its own albedo: tinting it by the light colour makes a dim
		// coloured "night sun" produce a dim coloured moon. The atmosphere above keeps
		// using the light colour, because that term really is the light's scattering.
		vColor += g_vMoonColor * g_flMoonBrightness * nightBlend * ( disc + halo );

		// Stars, washed out near the moon and behind its halo.
		float3 stars = StarField( vRay ) * nightBlend;

		// The wash is scaled by the blend too: an unscaled one punches a star-free hole
		// around a moon that is invisible at low night blend.
		float moonWash = saturate( disc * 1.5f + halo * 2.0f ) * nightBlend;
		stars *= saturate( 1.0f - moonWash );
		vColor += stars;

		o.vColor0 = float4( vColor, 1.0 );
		o.vColor0.rgb = ApplyVolumetricFog( o.vColor0.rgb, i.vPositionWs, i.vPositionSs.xy );
		return o;
	}
}
