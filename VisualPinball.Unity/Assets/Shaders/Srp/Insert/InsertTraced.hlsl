// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later
#ifndef VPE_INSERT_TRACED_INCLUDED
#define VPE_INSERT_TRACED_INCLUDED

// Traced mode of the hybrid insert, behind the material keyword _INSERT_TRACED.
// Without the keyword both functions here hand their inputs straight through,
// and the insert is the live one, as before.
//
// With it, the body's light comes from an offline render of the insert (a
// path-traced bulb under the real plastic, seen from four elevations and
// packed into one RGBA map, _TracedLight), instead of the soft and edge glow
// of the optical response map. Everything else stays the live material's:
// the colour, brightness and lamp drive, the scene-lit body and its room
// return, the cover. The bulb seen through the facets is still drawn live,
// because a render cannot know where the eye is: through a facet normal map
// traced with the light (_TracedFacets), or with the live material's own image.
//
// The traced light belongs to the top of the insert: each texel is what the
// eye sees through that point of the top from the traced elevation. So the
// body is shaded as a flat card at the top, whatever part of the mesh a
// pixel falls on. The cover refracts what lies under it as through a slab as
// thick as the insert is deep, which would carry a card at the top off by the
// slab's thickness times the difference of the view's tangent in air and in
// the plastic; the card is shaded where the cover will show it instead.
// The mesh is only what the card is drawn on, so nothing of the traced light
// moves with the facets the mesh happens to have: one parallax, the bake's.
//
// Insert space is the live material's: metres, y up, the top at y = 0, z
// along the length. The bake has its own square, _TracedFrame.w metres wide,
// placed in insert space by its centre (_TracedFrame.xy) and turned by
// _TracedFrame.z degrees about y; it was rendered from its own -z side.

#include "InsertOptics.hlsl"

// The lamp-off brightness, in stops (a global: 0, the default, is the bake as calibrated). The table sets it when it loads,
// to match the bake's room to its own, so the unlit inserts sit in the room instead of glowing on their own.
float _InsertTracedOffEv;

float2 InsertTracedTurn(float2 p, float degrees)
{
    float s, c;
    sincos(radians(degrees), s, c);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

// Insert space to the bake's square, as a texture coordinate.
float2 InsertTracedUv(float2 positionXZ)
{
    float2 bake = InsertTracedTurn(positionXZ - _TracedFrame.xy, _TracedFrame.z);
    return bake / max(_TracedFrame.w, 1e-4) + 0.5;
}

// How much each of the four traced elevations counts at this one: linear
// between neighbours, held at the ends rather than guessed beyond them.
float4 InsertTracedAngleWeights(float elevation)
{
    float4 a = _TracedAngles;
    float4 w = 0.0;
    if (elevation <= a.x) {
        w.x = 1.0;
    } else if (elevation <= a.y) {
        float t = (elevation - a.x) / max(a.y - a.x, 1e-3);
        w.xy = float2(1.0 - t, t);
    } else if (elevation <= a.z) {
        float t = (elevation - a.y) / max(a.z - a.y, 1e-3);
        w.yz = float2(1.0 - t, t);
    } else if (elevation <= a.w) {
        float t = (elevation - a.z) / max(a.w - a.z, 1e-3);
        w.zw = float2(1.0 - t, t);
    } else {
        w.w = 1.0;
    }
    return w;
}

// A facet normal of the bake, in insert space. The map holds the normal's x
// and z in red and green, in the bake's square, and in alpha whether the
// point is on the faceted floor at all.
float4 InsertTracedFacet(float2 positionXZ)
{
    float2 uv = InsertTracedUv(positionXZ);
    float4 f = SAMPLE_TEXTURE2D_GRAD(_TracedFacets, sampler_TracedFacets, uv, ddx(uv), ddy(uv));   // filtered: unfiltered facets crawl on a small insert
    float2 nxz = f.rg * 2.0 - 1.0;
    nxz = InsertTracedTurn(nxz, -_TracedFrame.z);
    return float4(normalize(float3(nxz.x, sqrt(saturate(1.0 - dot(nxz, nxz))), nxz.y)), f.a);
}

// The traced glints: the facets catching a few fixed highlights, each colour
// tilted a little apart, so a spoke flashes with a fringe as the eye moves.
// The highlights turn with the eye's bearing, read per pixel so a glint slides
// along a facet instead of switching whole facets. Straight under the eye the
// bearing is undefined and spins round that point (a radial seam, Ben
// 2026-10-20), so the glints fade out over the last ~15 degrees to vertical.
float InsertTracedGlint(float3 n, float3 v, float turn, float sharp, int channel)
{
    float4 p = _TracedGlintParams;
    int keys = (int)clamp(p.z, 1.0, 8.0);
    float tilt = radians(p.x + (channel - 1) * p.w);
    float st, ct;
    sincos(tilt, st, ct);
    float azimuth = atan2(v.z, v.x) + radians(_TracedGlintRot + turn);
    float sum = 0.0;
    for (int j = 0; j < keys; j++) {
        float sa, ca;
        sincos(azimuth + 6.28318530718 * j / keys, sa, ca);
        sum += pow(saturate(dot(n, float3(st * ca, ct, st * sa))), sharp);
    }
    // Energy kept as the lobe widens (Phong normalisation, against the default
    // sharpness of 600): a wide glint is a dim one, not a flood into the bloom.
    return sum * ((sharp + 2.0) / 602.0) * smoothstep(0.05, 0.25, length(v.xz));
}

// The traced lamp-off look: the bake's env pass, the insert under the room with the lamp off (refraction through the facets,
// the room in them, the coloured plastic), packed four elevations to a 2x2 atlas (_TracedEnv: 15 deg top left, 45 top right,
// 60 bottom left, 70 bottom right, as _TracedAngles), already multiplied by its body mask and calibrated. Read at the same card
// point and weights as the traced light, so it holds still against the plastic and crossfades with the view.
float3 InsertTracedEnv(float2 uv, float4 weights)
{
    // The mip level the insert's screen size asks for (with mipmaps imported), read by hand since the four slices share one
    // texture: half a texel of that level in from each slice's edge, so no slice bleeds into its neighbour. A slice is a
    // power-of-two block, so its mip texels never straddle two slices.
    float2 slice = _TracedEnv_TexelSize.zw * 0.5;
    float2 dx = ddx(uv) * slice, dy = ddy(uv) * slice;
    float lod = clamp(0.5 * log2(max(max(dot(dx, dx), dot(dy, dy)), 1e-8)), 0.0, 4.0);
    float2 margin = _TracedEnv_TexelSize.xy * exp2(lod);
    float2 q = clamp(uv, margin, 1.0 - margin) * 0.5;
    return SAMPLE_TEXTURE2D_LOD(_TracedEnv, sampler_TracedEnv, q + float2(0.0, 0.5), lod).rgb * weights.x
        + SAMPLE_TEXTURE2D_LOD(_TracedEnv, sampler_TracedEnv, q + float2(0.5, 0.5), lod).rgb * weights.y
        + SAMPLE_TEXTURE2D_LOD(_TracedEnv, sampler_TracedEnv, q, lod).rgb * weights.z
        + SAMPLE_TEXTURE2D_LOD(_TracedEnv, sampler_TracedEnv, q + float2(0.5, 0.0), lod).rgb * weights.w;
}

float3 InsertTracedEmission(float3 position, float3 view)
{
    float3 v = normalize(view + float3(0.0, 1e-5, 0.0));
    float eta = max(_OpticalIor, 1.001);
    float3 inside = refract(-v, float3(0.0, 1.0, 0.0), rcp(eta));
    float steep = max(v.y, 0.05);
    float steepInside = max(-inside.y, 0.05);
    // Where the eye's ray crosses the top, then where the cover will show it.
    float2 top = position.xz + v.xz * (-position.y / steep);
    float cover = _TracedGeometry.x * 0.001;
    float2 card = top - cover * (v.xz / steep + inside.xz / steepInside);

    // The elevation in the bake's frame, and how far the inside of the
    // plastic slides against the top away from the traced direction.
    float3 vb = float3(InsertTracedTurn(v.xz, _TracedFrame.z), v.y).xzy;
    float elevation = degrees(acos(saturate(vb.y)));
    float4 weights = InsertTracedAngleWeights(elevation);
    float3 tracedView = float3(0.0, cos(radians(elevation)), -sin(radians(elevation)));
    float3 tracedInside = refract(-tracedView, float3(0.0, 1.0, 0.0), rcp(eta));
    float3 vbInside = refract(-vb, float3(0.0, 1.0, 0.0), rcp(eta));
    float2 slide = (vbInside.xz / max(-vbInside.y, 0.25) - tracedInside.xz / max(-tracedInside.y, 0.25))
        * (_TracedGeometry.z * 0.001 / max(_TracedFrame.w, 1e-4));
    // Filtered by the screen size of the insert, so a small one doesn't shimmer (with mipmaps imported).
    float2 lightUv = InsertTracedUv(card) + slide;
    float light = dot(SAMPLE_TEXTURE2D_GRAD(_TracedLight, sampler_TracedLight, lightUv, ddx(lightUv), ddy(lightUv)), weights);
    // Glow Spread: one is the render as baked. The traced light is normalised to its brightest texels, so a higher spread
    // lifts the dim reaches toward the core (the glow fills the insert instead of pooling over the bulb), a lower one tightens it.
    light = pow(max(light, 0.0), rcp(max(_TracedGlowSpread, 0.05)));

    // Seen at a slant, the render shows the pocket's far wall and the skirt
    // as a bright band just inside the outline, which a flat card puts on the
    // top. Fade the light out over the last _TracedGeometry.w millimetres
    // inside the outline: the floor mask, blurred over that width, is a half
    // on the outline and whole about a width in.
    if (_TracedGeometry.w > 0.0) {
        float texel = max(_TracedFrame.w, 1e-4) * _TracedFacets_TexelSize.x;
        float mip = log2(max(2.0 * _TracedGeometry.w * 0.001 / texel, 1.0));
        float inside = SAMPLE_TEXTURE2D_LOD(_TracedFacets, sampler_TracedFacets, InsertTracedUv(card), mip).a;
        light *= smoothstep(0.5, 0.95, inside);
    }

    // The live material's drive: each bulb its colour and level, reaching as
    // far as its spread lets it, as the live glow sums them.
    float output = max(_InsertLampOutput, 0.0);
    float4 lamps[4] = { _OpticalLamp, _OpticalLampB, _OpticalLampC, _OpticalLampD };
    float3 drives[4] = { max(_InsertLampColor.rgb, 0.0) * saturate(_InsertLampLevel) * output,
        max(_InsertLampColorB.rgb, 0.0) * saturate(_InsertLampColorB.w) * output,
        max(_InsertLampColorC.rgb, 0.0) * saturate(_InsertLampColorC.w) * output,
        max(_InsertLampColorD.rgb, 0.0) * saturate(_InsertLampColorD.w) * output };

    // The bulb through the facets, as the live material draws it, from the
    // card: down through the plastic to the facets, out through the facet
    // the bake found there, on to the bulb.
    float facetDepth = _TracedGeometry.y * 0.001;
    float3 floorPoint = float3(card.x, 0.0, card.y) + inside * (facetDepth / steepInside);
    float4 facet = InsertTracedFacet(floorPoint.xz);
    float3 n = normalize(float3(facet.x, max(abs(facet.y), 0.25), facet.z));
    float reflectance0 = (eta - 1.0) / (eta + 1.0);
    reflectance0 *= reflectance0;
    float cosInside = saturate(dot(-inside, n));
    float sinFacet = eta * sqrt(saturate(1.0 - cosInside * cosInside));
    float passing = 1.0 - smoothstep(0.75, 1.0, sinFacet);
    float3 facetRay = passing > 0.0 ? refract(inside, n, eta) : -v;
    float cosFacet = saturate(dot(facetRay, -n));
    float facetExit = 1.0 - (reflectance0 + (1.0 - reflectance0) * pow(1.0 - cosFacet, 5.0));
    float flatExit = 1.0 - (reflectance0 + (1.0 - reflectance0) * pow(1.0 - saturate(v.y), 5.0));
    float3 ray = normalize(lerp(-v, facetRay, passing));
    float exit = lerp(flatExit, facetExit, passing);
    float fresnel = 0.04 + 0.96 * pow(1.0 - saturate(dot(v, n)), 5.0);
    bool ownImage = _TracedBulbSource >= 0.5;   // the complement of InsertTracedPre's liveImage
    float depth = max(_TracedBulbDepth, 0.0);

    float3 glowDrive = 0.0;
    float3 bulbs = 0.0;
    [unroll] for (int i = 0; i < 4; i++) {
        if (i > 0 && lamps[i].w <= 0.0) {
            continue;
        }
        float4 lamp = float4(lamps[i].x, lamps[i].y * depth, lamps[i].z, lamps[i].w);
        float4 b = InsertOpticsBulb(lamp, floorPoint, ray, 1.0, fresnel, exit, facet.w, _InsertCavitySpread, _OpticalSpread);
        // The glow is driven from where the bulb really is: Bulb Depth only moves its image.
        float reach = depth == 1.0 ? b.z
            : InsertOpticsBulb(lamps[i], floorPoint, ray, 1.0, fresnel, exit, facet.w, _InsertCavitySpread, _OpticalSpread).z;
        glowDrive += reach * drives[i];
        // The tight image of the bulb, and as much of the broad halo the live
        // material draws through each facet as asked: the halo is what moves
        // with the eye, but part of its light is in the traced render already.
        bulbs += (b.w + saturate(_TracedBulbHalo) * b.x) * b.z * drives[i];
    }

    float wear = saturate(_InsertRoughness);
    float3 grainAt = float3(card.x, 0.0, card.y) * 4000.0 + _OpticalSeed * float3(13.1, 17.7, 11.3);
    float finish = 1.0 + (InsertGrain(grainAt) - 0.5) * InsertDetailVisibility(grainAt) * _OpticalGrain;

    // No two inserts glint alike: a seed from where the insert sits on the
    // table turns its highlights a little (Glint Organic y, degrees either way)
    // and shifts the moulding's waviness (x degrees of tilt over z millimetres),
    // so a row of the same insert catches the light each its own way.
    float3 originWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
#if defined(SHADEROPTIONS_CAMERA_RELATIVE_RENDERING) && (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    originWS = GetAbsolutePositionWS(originWS);   // HDRP: world space is camera-relative
#endif
    float seed = frac(sin(dot(originWS.xz, float2(127.1, 311.7))) * 43758.5453);
    float seedB = frac(seed * 17.13 + 0.37);
    float turn = (seed * 2.0 - 1.0) * _TracedGlintOrganic.y;
    float2 wave = card * (6.28318530718 / max(_TracedGlintOrganic.z * 0.001, 1e-4));
    float2 wobble = float2(sin(wave.x * 0.83 + wave.y * 0.57 + seed * 6.283), sin(wave.y * 0.91 - wave.x * 0.41 + seedB * 6.283));
    float2 lean = wobble * tan(radians(clamp(_TracedGlintOrganic.x, 0.0, 30.0)));
    float3 wavy = normalize(facet.xyz + float3(lean.x, 0.0, lean.y));
    // Specular anti-aliasing: where facets are smaller than a pixel the
    // highlight widens by how much the unfiltered facet normal changes across
    // the pixel, so glints soften with distance instead of crawling.
    float2 facetUv = InsertTracedUv(floorPoint.xz);
    float2 rawRg = SAMPLE_TEXTURE2D_LOD(_TracedFacets, sampler_TracedFacets, facetUv, 0).rg * 2.0 - 1.0;
    float variance = max(dot(ddx(rawRg), ddx(rawRg)), dot(ddy(rawRg), ddy(rawRg)));
    float alpha2 = 2.0 / (max(_TracedGlintParams.y, 1.0) + 2.0) + min(0.5 * variance, 0.18);
    float sharp = max(2.0 / alpha2 - 2.0, 1.0);
    float3 glint = float3(InsertTracedGlint(wavy, v, turn, sharp, 0), InsertTracedGlint(wavy, v, turn, sharp, 1),
        InsertTracedGlint(wavy, v, turn, sharp, 2)) * facet.w;

    // The traced light is normalised to its own brightest texels; twenty
    // times it, in units of the live Scatter, is about as bright as the
    // live look on a typical insert, so Traced Glow starts near one.
    float glow = 20.0 * max(_OpticalScatter, 0.0) * max(_TracedGlow, 0.0) * (1.0 - saturate(_TracedLiveMix));
    float3 emission = max(_InsertLuminosity, 0.0) * max(_EmissiveColor.rgb, 0.0) * finish
        * (glowDrive * ((1.0 + 0.5 * wear) * glow * light + 10.0 * max(_TracedGlint, 0.0) * glint)
        + (ownImage ? max(_OpticalLampGain, 0.0) * max(_TracedBulbGain, 0.0) * (1.0 - wear) * bulbs : 0.0));

    // Alignment view: the bake's square in a 2 mm grid, red outside it,
    // the traced light in green.
    if (_TracedDebug > 0.5) {
        float2 uv = InsertTracedUv(card);
        float2 mm = frac(uv * max(_TracedFrame.w, 1e-4) * 500.0);
        float grid = step(0.9, max(mm.x, mm.y));
        float outside = any(uv < 0.0) || any(uv > 1.0) ? 1.0 : 0.0;
        emission = float3(outside, light, grid) * 1000.0;
    }
    // The lamp-off look is always there, under the lamp, as the bake's two passes add: it replaces the live body's unlit colour
    // (InsertTraced below) as far as Lamp-Off Look goes, and the lamp's glow adds over it.
    // The lamp-off render is one image per insert type, so on its own a row of
    // them is identical (Ben, 2026-10-20). Each insert reads it from where it
    // sits: its own few degrees of elevation (Lamp-Off Vary x), its own
    // brightness (y, a fraction either way) and a wobble of the refraction
    // with the moulding's waviness (z, in texels). And the room glints in the
    // facets as the lamp does, with the same per-insert turn and waviness, as
    // strong as the room light the render shows there (Lamp-Off Glints).
    float look = max(_TracedEnvGain, 0.0);
    if (look > 0.0 && _TracedDebug < 0.5) {
        float4 vary = _TracedEnvVary;
        float4 envWeights = InsertTracedAngleWeights(elevation + (seed * 2.0 - 1.0) * vary.x);
        float2 envUv = InsertTracedUv(card) + slide + wobble * vary.z * 2.0 * _TracedEnv_TexelSize.xy;
        float3 env = InsertTracedEnv(envUv, envWeights) * max(1.0 + (seedB * 2.0 - 1.0) * vary.y, 0.0);
        emission += look * exp2(_InsertTracedOffEv) * (env + 10.0 * max(_TracedGlint, 0.0) * max(_TracedOffGlint, 0.0) * env * glint);
    }
#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
    emission *= _InsertUrpEmissionScale;
#endif
    return emission;
}

// Before the live optics: in traced mode the live glow is kept only as far as
// the mix asks, and the live bulb image only when it is the one drawn, with
// the bulbs as deep as asked.
void InsertTracedPre_float(float Scatter, float EdgeGlow, float LampGain, float4 Lamp, float4 LampB, float4 LampC, float4 LampD,
    out float ScatterOut, out float EdgeGlowOut, out float LampGainOut,
    out float4 LampOut, out float4 LampBOut, out float4 LampCOut, out float4 LampDOut)
{
#if defined(_INSERT_TRACED)
    float mix = saturate(_TracedLiveMix);
    bool liveImage = _TracedBulbSource < 0.5;
    float depth = liveImage ? max(_TracedBulbDepth, 0.0) : 1.0;
    ScatterOut = Scatter * mix;
    EdgeGlowOut = EdgeGlow * mix;
    LampGainOut = liveImage ? LampGain * max(_TracedBulbGain, 0.0) : 0.0;
    LampOut = float4(Lamp.x, Lamp.y * depth, Lamp.zw);
    LampBOut = float4(LampB.x, LampB.y * depth, LampB.zw);
    LampCOut = float4(LampC.x, LampC.y * depth, LampC.zw);
    LampDOut = float4(LampD.x, LampD.y * depth, LampD.zw);
#else
    ScatterOut = Scatter;
    EdgeGlowOut = EdgeGlow;
    LampGainOut = LampGain;
    LampOut = Lamp;
    LampBOut = LampB;
    LampCOut = LampC;
    LampDOut = LampD;
#endif
}

// After the live optics: the traced light, the glints and the bulb through the
// traced facets, added to what the live optics still give.
void InsertTraced_float(float3 Position, float3 View, float3 BaseColorIn, float3 NormalIn, float3 EmissionIn,
    float3 WorldNormal, float3 WorldTangent, float3 WorldBitangent,
    out float3 BaseColor, out float3 NormalTS, out float3 Emission)
{
    BaseColor = BaseColorIn;
    NormalTS = NormalIn;
    Emission = EmissionIn;
#if defined(_INSERT_TRACED) && !defined(SHADERGRAPH_PREVIEW)
    BaseColor *= 1.0 - saturate(_TracedEnvGain);   // the traced lamp-off look takes the live unlit body's place
    // Mesh Facet Normals: one keeps the live mesh's facet normals for the scene's light on the body; zero gives the body the
    // flat polished top a real insert has (its facets are underneath, and the traced maps already draw them). The top is the
    // insert's up, written in each face's own tangent frame, as InsertOptics flattens towards it.
    float3 upWS = TransformObjectToWorldNormal(float3(0.0, 1.0, 0.0));
    float3 upTS = TransformWorldToTangent(upWS, float3x3(WorldTangent, WorldBitangent, WorldNormal));
    NormalTS = normalize(lerp(upTS, NormalIn, saturate(_TracedMeshNormals)));
    Emission += InsertTracedEmission(Position, View);
#endif
}
#endif
