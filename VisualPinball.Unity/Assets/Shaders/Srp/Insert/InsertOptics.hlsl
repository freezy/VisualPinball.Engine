// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later
#ifndef VPE_INSERT_OPTICS_INCLUDED
#define VPE_INSERT_OPTICS_INCLUDED

// Read the sky in both pipelines, rather than a local reflection probe.
// Decode URP's environment texture before comparing room brightness.
float3 InsertSampleRoom(float3 direction, float mip)
{
#if defined(SHADERGRAPH_PREVIEW)
    return 1.0;
#elif defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
    float4 encoded = SAMPLE_TEXTURECUBE_LOD(_GlossyEnvironmentCubeMap, sampler_GlossyEnvironmentCubeMap, direction, mip);
    return DecodeHDREnvironment(encoded, _GlossyEnvironmentCubeMap_HDR);
#else
    return SampleSkyTexture(direction, mip, 0).rgb;
#endif
}

float3 InsertFacetRandom(float sector, float seed)
{
    return frac(sin((sector + seed * 17.13) * float3(127.1, 311.7, 74.7)) * 43758.5453);
}

float3 InsertNoiseHash(float3 p)
{
    float3 h = frac(p * float3(0.1031, 0.1030, 0.0973));
    h += dot(h, h.yxz + 33.33);
    return frac((h.xxy + h.yxx) * h.zyx);
}

// Soft nearest-cell values: small cloudy variations without hard Voronoi
// borders. Coordinates are local metres, in all three axes, so atlas seams
// cannot split them and no slope stretches them: a starburst's facets stand
// steep, and a noise laid out over the floor with a share of the height
// mixed in was sheared along one slope of every ridge and squeezed on the
// other, into streaks along the facets seen on every other pleat. Noise in
// three dimensions is the same on any face, walls included.
float InsertCellular(float3 p)
{
    float3 cell = floor(p);
    float3 local = frac(p);
    float value = 0.0;
    float weight = 0.0;
    [unroll] for (int z = -1; z <= 1; z++) {
        [unroll] for (int y = -1; y <= 1; y++) {
            [unroll] for (int x = -1; x <= 1; x++) {
                float3 offset = float3(x, y, z);
                float3 random = InsertNoiseHash(cell + offset);
                float3 delta = offset + 0.15 + 0.7 * random - local;
                float w = exp2(-8.0 * dot(delta, delta));
                value += InsertNoiseHash(cell + offset + 17.0).x * w;
                weight += w;
            }
        }
    }
    return value / max(weight, 1e-5);
}

float InsertGrain(float3 p)
{
    float3 cell = floor(p);
    float3 t = frac(p);
    t = t * t * (3.0 - 2.0 * t);
    float4 below = float4(InsertNoiseHash(cell).z, InsertNoiseHash(cell + float3(1, 0, 0)).z,
        InsertNoiseHash(cell + float3(0, 1, 0)).z, InsertNoiseHash(cell + float3(1, 1, 0)).z);
    float4 above = float4(InsertNoiseHash(cell + float3(0, 0, 1)).z, InsertNoiseHash(cell + float3(1, 0, 1)).z,
        InsertNoiseHash(cell + float3(0, 1, 1)).z, InsertNoiseHash(cell + 1.0).z);
    float4 layer = lerp(below, above, t.z);
    return lerp(lerp(layer.x, layer.y, t.x), lerp(layer.z, layer.w, t.x), t.y);
}

// The height of the surface grain, two octaves of the value noise like the
// bake's, in cells.
float InsertSurfaceGrainHeight(float3 cells)
{
    return InsertGrain(cells) + 0.5 * InsertGrain(cells * 2.07 + 17.3);
}

// Ray tracing stages have no screen-space derivatives, so the detail stays
// fully visible there.
float InsertDetailVisibility(float3 p)
{
#if defined(SHADER_STAGE_RAY_TRACING)
    return 1.0;
#else
    float footprint = max(length(ddx(p)), length(ddy(p)));
    return 1.0 - smoothstep(0.35, 1.0, footprint);
#endif
}

float InsertPixelWidth(float x)
{
#if defined(SHADER_STAGE_RAY_TRACING)
    return 0.0;
#else
    return fwidth(x);
#endif
}

// All positions and directions are in insert local space, in metres. This
// works per eye and at arbitrary camera angles without a scene-colour buffer.
// Hue, saturation and value, hue as a turn of the wheel in 0..1.
float3 InsertOpticsRgbToHsv(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 InsertOpticsHsvToRgb(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}

// Turns the colour's hue towards the lamp's, and its saturation towards the
// lamp's, never below its own, both by the amount; the value is never touched, so a grey haze comes out the lamp's
// colour, as bright as the grey was, and never darker. An RGB lamp carries
// its intensity in its colour, and a dim lamp is still its colour: the amount
// is full from a tenth of full brightness up, and only a lamp that is off,
// its channels all down, leaves the colour untouched. A white lamp has no hue
// to give.
float3 InsertOpticsTowardsLampHue(float3 color, float3 lamp, float amount)
{
    float3 hsv = InsertOpticsRgbToHsv(max(color, 0.0));
    float3 lampHsv = InsertOpticsRgbToHsv(max(lamp, 0.0));
    amount = saturate(amount) * saturate(lampHsv.z * 10.0);
    // The hue turns the short way round the wheel, by the lamp's share of the
    // saturation the mix ends up with: a grey, which has no hue of its own,
    // takes the lamp's hue at any amount, its saturation growing with it,
    // instead of a hue part of the way round the wheel, so a grey haze half
    // tinted by a blue lamp is a paler blue, never pink; two colours as
    // saturated as each other turn by the amount itself.
    float turn = frac(lampHsv.x - hsv.x + 0.5) - 0.5;
    float share = saturate(amount * lampHsv.y / max(lerp(hsv.y, lampHsv.y, amount), 1e-5));
    hsv.x = frac(hsv.x + turn * share);
    hsv.y = lerp(hsv.y, max(hsv.y, lampHsv.y), amount);
    return InsertOpticsHsvToRgb(hsv);
}

// One virtual bulb's share of a point: the halo its facet shows (x), how much
// of the cavity under it a clear body's face looks onto (y), how far the point
// sits from it for the spread (z, one without a spread), and the tight image
// of the bulb itself (w), kept apart from the halo so it can take a colour of
// its own. Position and the refracted view ray are in insert space, the lamp
// its position in metres with its radius in w.
float4 InsertOpticsBulb(float4 lamp, float3 position, float3 ray, float faceGain, float fresnel, float exit,
    float responseB, float cavitySpread, float spread)
{
    float distance = max(position.y - lamp.y, 0.0) / max(-ray.y, 0.12);
    float2 hit = position.xz + ray.xz * distance;
    float radius = max(abs(lamp.w), 0.0001);
    // Keep a common source behind every face. Moving the source independently
    // per sector turns the bulb into unrelated gradients and destroys depth.
    float2 delta = (hit - lamp.xz) / radius;
    float direct = exp2(-2.0 * dot(delta, delta));
    // A tighter image of the bulb inside its halo, as strong as the looks
    // always had it.
    float bulb = exp2(-12.0 * dot(delta, delta)) * 3.7;
    // A wider reflected image stands in for a second internal bounce.
    float2 reflectedHit = position.xz - ray.xz * distance * 0.65;
    float2 reflectedDelta = (reflectedHit - lamp.xz) / (radius * 1.5);
    float bounced = exp2(-2.0 * dot(reflectedDelta, reflectedDelta));
    // The tight source image survives even on the less efficient slopes;
    // the broad transmitted component still carries face-to-face contrast.
    // Both are what the facet lets through, none at all once it traps the
    // ray; the bounce is what it throws back and stays.
    float passed = responseB * (1.0 - 0.65 * fresnel);
    float transmitted = passed * (direct * exit + 0.17 * bounced) * faceGain;
    float image = passed * bulb * exit * lerp(1.0, faceGain, 0.25);
    // Each face shows the part of the cavity its refracted view ray lands on,
    // spread wide and kept soft, so the ribs shift with the view instead of
    // alternating with the lights.
    float wide = radius * max(cavitySpread, 0.1);
    float2 cavityDelta = (hit - lamp.xz) / wide;
    float2 cavityBounce = (reflectedHit - lamp.xz) / (wide * 1.5);
    float seen = exit * exp2(-dot(cavityDelta, cavityDelta)) + 0.3 * exp2(-dot(cavityBounce, cavityBounce));
    // Every facet tilted towards the lamp shows its halo, however far from the
    // lamp it sits, and the baked glow reaches the whole body alike, so raising
    // the brightness floods the body. A real insert is brightest around its
    // bulb and dims away from it, the light spreading out from the bulb as
    // from a point, by the inverse square of the distance: the spread, in
    // millimetres, is the distance from the lamp at which the lit look has
    // fallen to half, a fifth at twice that and a tenth at three times, a long
    // reach so that one bulb of a banana still shows at its far end and two
    // bulbs meet in a soft blend, never a seam. Zero leaves it everywhere alike.
    float leak = 1.0;
    if (spread > 0.0) {
        float2 away = (position.xz - lamp.xz) / (spread * 0.001);
        leak = rcp(1.0 + dot(away, away));
    }
    return float4(transmitted, seen, leak, image);
}

// What a facet reflects of the light inside the plastic, seen from the dense
// side: Schlick on the angle in the air beyond it, and all of it once the
// facet traps the ray.
float InsertInternalReflectance(float cosInside, float eta, float reflectance0)
{
    float sinOutside = eta * sqrt(saturate(1.0 - cosInside * cosInside));
    if (sinOutside >= 1.0) {
        return 1.0;
    }
    float cosOutside = sqrt(saturate(1.0 - sinOutside * sinOutside));
    return reflectance0 + (1.0 - reflectance0) * pow(1.0 - cosOutside, 5.0);
}

// Whether a ray inside the plastic, pointing up, gets out through the flat
// top: not past the critical angle, faded over the last tenth so nothing
// jumps as the eye moves.
float InsertEscapes(float3 ray, float eta)
{
    float sinOutside2 = eta * eta * (1.0 - ray.y * ray.y);
    return step(0.0, ray.y) * (1.0 - smoothstep(0.8, 1.0, sinOutside2));
}

// The room, as a ray leaving through the top sees it: bent out of the
// plastic, turned to world space, and read from the sky's reflection cubemap
// at a blurred mip, which is what a moulded, slightly rough groove returns.
float3 InsertRoom(float3 ray, float eta, float blur)
{
    float3 outside = refract(ray, float3(0.0, -1.0, 0.0), eta);
    float3 world = TransformObjectToWorldDir(outside + float3(0.0, 1e-5, 0.0));
    return InsertSampleRoom(world, blur);
}

// The sky's average brightness round the insert: its most blurred mip
// straight up and to the four sides, the hemisphere a groove returns.
float InsertSkyAverage()
{
    float3 lum = float3(0.2126, 0.7152, 0.0722);
    float sum = dot(InsertSampleRoom(float3(0.0, 1.0, 0.0), 6.0), lum)
        + dot(InsertSampleRoom(float3(1.0, 0.0, 0.0), 6.0), lum)
        + dot(InsertSampleRoom(float3(-1.0, 0.0, 0.0), 6.0), lum)
        + dot(InsertSampleRoom(float3(0.0, 0.0, 1.0), 6.0), lum)
        + dot(InsertSampleRoom(float3(0.0, 0.0, -1.0), 6.0), lum);
    return max(sum * 0.2, 1e-6);
}

// How much of the room a ray going down into a groove brings back out
// through the top, and, where the sky may be read, the room it brings. The
// groove is the facet the ray meets first and its mirrored neighbour. After
// each facet the ray goes on to the other one if it heads into it, which is
// when it climbs more slowly than that facet rises; otherwise it goes up to
// the top, where what gets out is the room seen that way and the rest comes
// back down, under total internal reflection all of it, onto whichever
// facet it heads into. A groove near a right angle returns the ray off the
// two facets, a corner reflector; a shallower one throws it up to the top,
// which sends it down onto the other facet, and that one out. Each facet
// takes its Fresnel share; four facets are as far as it goes. Where the ray
// comes down from the top as squarely onto one facet as onto the other, the
// choice between them would flip the rest of its path from one side of the
// room to the other within a facet: what comes back after such a choice is
// faded in as the choice firms up, so nothing steps. The room is
// the sky over its average brightness: how much brighter or darker it is
// that way than all round. Where the sky isn't read, it is the same all round.
float InsertRoomPaths(float3 ray, float3 bent, float3 neighbour, float eta, float reflectance0, float blur, bool readRoom, out float3 room)
{
    room = float3(0.0, 0.0, 0.0);
    float back = 0.0;
    float carried = 1.0;
    // How firmly the path's facet choices were made, fading what follows a near tie.
    float firm = 1.0;
    float3 d = ray;
    bool onBent = true;
    for (int i = 0; i < 4; i++) {
        float3 facet = onBent ? bent : neighbour;
        float3 other = onBent ? neighbour : bent;
        carried *= InsertInternalReflectance(saturate(dot(-d, facet)), eta, reflectance0);
        d = reflect(d, facet);
        if (dot(-d, other) > 0.0) {
            onBent = !onBent;
            continue;
        }
        if (d.y <= 0.0) {
            break;
        }
        float escaping = InsertEscapes(d, eta) * (1.0 - InsertInternalReflectance(saturate(d.y), eta, reflectance0));
        float leaving = carried * escaping;
        carried -= leaving;
        leaving *= firm;
        back += leaving;
        if (readRoom && leaving > 0.0) {
            room += leaving * InsertRoom(d, eta, blur);
        } else if (!readRoom) {
            room += leaving;
        }
        d.y = -d.y;
        float towardsBent = dot(-d, bent);
        float towardsNeighbour = dot(-d, neighbour);
        if (max(towardsBent, towardsNeighbour) <= 0.0) {
            break;
        }
        onBent = towardsBent >= towardsNeighbour;
        firm *= smoothstep(0.0, 0.05, abs(towardsBent - towardsNeighbour));
    }
    if (readRoom && back > 0.0) {
        room /= InsertSkyAverage();
    }
    return back;
}

void InsertOptics_float(float3 Position, float3 View, float2 UV,
    UnityTexture2D OpticalNormal, UnityTexture2D Response, UnityTexture2D SurfaceNormal,
    float4 Lamp, float Ior, float Scatter, float EdgeGlow, float LampGain,
    float NormalStrength, float3 Tint, float3 LampColor, float3 DriveColor, float Level,
    float4 Starburst, float Contrast, float Variation, float Seed, float Brightness,
    float MottleSize, float Grain, float MinBrightness,
    float3 WorldNormal, float3 WorldTangent, float3 WorldBitangent, float Flatten,
    float Clarity, float3 CavityColor, float CavitySpread, float CavityContrast,
    float LampTint, float Spread, float4 LampB, float4 LampC, float4 LampD,
    float4 DriveB, float4 DriveC, float4 DriveD,
    float SurfaceGrain, float SurfaceGrainSize, float Output, float Roughness,
    float Flecks, float FleckSize, float RoomReturn, float RoomBlur, float RoomReturnEV, float UrpEmissionScale,
    out float3 BaseColor, out float3 NormalTS, out float3 Emission)
{
    // How rough the structure inside the plastic is: smooth facets and
    // channels couple the lamp into crisp ribs and rays, rough ones diffuse
    // it, so the ribs soften and the lamp's rays and image go into the glow.
    // The top's own finish is the cover's business, not this.
    float wear = saturate(Roughness);
    Contrast *= 1.0 - 0.7 * wear;
    float3 rawNormal = SAMPLE_TEXTURE2D(OpticalNormal.tex, OpticalNormal.samplerstate, UV).rgb * 2.0 - 1.0;
    float3 n = normalize(rawNormal + float3(0, 1e-5, 0));
    float3 response = SAMPLE_TEXTURE2D(Response.tex, Response.samplerstate, UV).rgb;
    float3 v = normalize(View + float3(0, 1e-5, 0));
    // The facets the bake laid out, found again from the position: a wheel's
    // sectors round its centre, ribs' strips across their direction, which
    // the bake tells apart by a half pitch made negative in z, or rings'
    // strips out from their centre, the half pitch pushed past minus a
    // thousand. Strips are counted from far off, so the index stays positive
    // and the neighbours either side need no wrapping round. A gem, its sides
    // pushed past two thousand, has facets no angle alone finds again, so its
    // variation turns round the centre smoothly, one value a side, with no
    // step to land in the middle of a facet.
    bool ringed = Starburst.z <= -1000.0;
    bool ribbed = Starburst.z < 0.0 && !ringed;
    bool gem = Starburst.z >= 2000.0;
    float sectors = Starburst.z < 0.0 ? 1e6 : gem ? max(Starburst.z - 2000.0, 1.0) : max(Starburst.z, 1.0);
    float coordinate;
    if (ringed) {
        coordinate = length(Position.xz - Starburst.xy) / max(-Starburst.z - 1000.0, 1e-6) + 4096.0;
    } else if (ribbed) {
        coordinate = dot(Position.xz, Starburst.xy) / -Starburst.z + 4096.0;
    } else {
        float2 radial = Position.xz - Starburst.xy;
        float angle = atan2(radial.y, radial.x);
        coordinate = frac(angle * (1.0 / 6.28318530718) + 1.0) * sectors;
    }
    float sector = floor(coordinate);
    float3 random = InsertFacetRandom(sector, Seed);
    // Fade the random coefficients across less than one screen pixel at a
    // crease. Their identity comes from the fan, never quantized bake noise.
    float width = gem ? 0.5 : min(0.45, max(InsertPixelWidth(coordinate), 0.01));
    float part = frac(coordinate);
    float previous = fmod(sector + sectors - 1.0, sectors);
    float next = fmod(sector + 1.0, sectors);
    random = lerp(InsertFacetRandom(previous, Seed), random, smoothstep(-width, width, part));
    random = lerp(random, InsertFacetRandom(next, Seed), smoothstep(1.0 - width, 1.0 + width, part));
    float floorMask = (Starburst.z < 0.0 ? 1.0 : step(4.0, Starburst.z)) * smoothstep(0.35, 0.7, response.b)
        * smoothstep(Starburst.w - 0.00015, Starburst.w + 0.00015, Position.y);
    float3 cells = Position / max(MottleSize * 0.001, 0.0001) + Seed * float3(7.13, 3.71, 5.29);
    float mottle = (InsertCellular(cells) - 0.5) * InsertDetailVisibility(cells);
    float3 grainPosition = Position * 4000.0 + Seed * float3(13.1, 17.7, 11.3);
    float grain = (InsertGrain(grainPosition) - 0.5) * InsertDetailVisibility(grainPosition);
    float finish = 1.0 + grain * Grain;
    // The flecks of a moulded insert: the ridges between the facets never
    // come out of the mould clean, and under room light every nick and
    // every bit of flash along them catches the light as a bright dash,
    // stretched along the rib it sits on, while the faces between stay
    // plain. Sparse cells of the cloudy noise, drawn long along the ribs
    // and gathered to the creases. Each is a small tilted face in the
    // plastic: its slope tilts the shading normal, so a fleck lights up
    // when a light or the room reflects off it and goes quiet when none
    // does, and only a little of it brightens the plastic flat, lit and
    // unlit, so the dashes are there in any light. Zero leaves a clean
    // moulding.
    float2 fleckSlope = float2(0.0, 0.0);
    if (Flecks > 0.0) {
        float2 radial = Position.xz - Starburst.xy;
        radial *= rsqrt(max(dot(radial, radial), 1e-10));
        float2 along = ringed ? float2(-radial.y, radial.x)
            : ribbed ? normalize(float2(-Starburst.y, Starburst.x) + float2(1e-6, 0.0))
            : gem ? float2(1.0, 0.0) : radial;
        float2 across = float2(-along.y, along.x);
        float size = max(FleckSize * 0.001, 1e-5);
        // Across a radial rib the position has no component of its own, so
        // the facet's index moves each rib to cells of its own.
        float3 fleckCells = float3(dot(Position.xz, along) / (size * 4.0), Position.y / size, dot(Position.xz, across) / size + sector * 3.7)
            + Seed * float3(5.1, 2.3, 8.7);
        float crease = 1.0 - smoothstep(0.0, 0.4, min(part, 1.0 - part));
        float weight = lerp(0.5, 1.0, crease) * floorMask * InsertDetailVisibility(fleckCells);
        // The value noise, not the cloudy cellular one: the three samples
        // here cost a fraction of the cellular's twenty-seven cells each,
        // and thresholded they give the same dashes.
        float here = smoothstep(0.42, 0.6, InsertGrain(fleckCells));
        // The slope of the dashes along the tangent and bitangent the normal
        // map is read in, a tenth of a cell apart, in the stretched cells.
        float3 tangentOS = normalize(TransformWorldToObjectDir(WorldTangent) + float3(1e-6, 0.0, 0.0));
        float3 bitangentOS = normalize(TransformWorldToObjectDir(WorldBitangent) + float3(0.0, 1e-6, 0.0));
        const float stepCells = 0.1;
        float3 alongT = stepCells * float3(dot(tangentOS.xz, along) * 0.25, tangentOS.y, dot(tangentOS.xz, across));
        float3 alongB = stepCells * float3(dot(bitangentOS.xz, along) * 0.25, bitangentOS.y, dot(bitangentOS.xz, across));
        float heightT = smoothstep(0.42, 0.6, InsertGrain(fleckCells + alongT));
        float heightB = smoothstep(0.42, 0.6, InsertGrain(fleckCells + alongB));
        fleckSlope = float2(heightT - here, heightB - here) / stepCells * weight;
        finish *= 1.0 + 0.35 * here * weight * saturate(Flecks);
    }
    // The facets bend the ray as the bake found them. Flattening steep walls
    // keeps this deliberately artistic ray pointed down.
    n = normalize(float3(n.x, max(abs(n.y), 0.25), n.z));
    // The view ray enters the plastic through its flat top, bending towards
    // the vertical, and leaves it through the facet underneath into the air
    // of the cavity, bending back the other way, the facet a prism, before it
    // runs on down to the bulb. Traced so, the bulb sits its full depth below
    // the insert: it shifts against the body by the whole of the depth times
    // the view's tilt as the eye moves, and each eye of a headset sees it
    // where its own ray meets it. One refraction with the facet's normal kept
    // the steeper in-plastic ray all the way down, half the parallax, and bent
    // it the wrong way round the facet. What the facet lets through falls
    // with the exit's Fresnel reflectance.
    //
    // A facet steep enough traps the ray, total internal reflection, or lets
    // it out so near the grazing that it runs off sideways and never meets
    // the lamp: a starburst of narrow spokes over a tall ridge has such
    // facets everywhere, and seen along its length, where the in-plastic ray
    // crosses every facet at its full tilt, the whole bulb went missing while
    // it showed from the side. Through a V groove that steep a trapped ray
    // leaves by the facet next door, thrown out to the side, so a mirrored
    // path would not find the bulb either. What such a facet would trap or
    // throw away goes straight on instead, as through a flat bottom, blended
    // in as the facet's exit nears the critical angle so nothing jumps: the
    // bulb keeps its place and its parallax from every angle, and the facets
    // keep their prism wherever they pass the ray.
    float eta = max(Ior, 1.001);
    float3 inside = refract(-v, float3(0.0, 1.0, 0.0), rcp(eta));
    float reflectance0 = (eta - 1.0) / (eta + 1.0);
    reflectance0 *= reflectance0;
    // The sine of the facet's exit angle, past one where it traps the ray.
    float cosInside = saturate(dot(-inside, n));
    float sinFacet = eta * sqrt(saturate(1.0 - cosInside * cosInside));
    float passing = 1.0 - smoothstep(0.75, 1.0, sinFacet);
    float3 facetRay = passing > 0.0 ? refract(inside, n, eta) : -v;
    float cosFacet = saturate(dot(facetRay, -n));
    float facetExit = 1.0 - (reflectance0 + (1.0 - reflectance0) * pow(1.0 - cosFacet, 5.0));
    float flatExit = 1.0 - (reflectance0 + (1.0 - reflectance0) * pow(1.0 - saturate(v.y), 5.0));
    float3 ray = normalize(lerp(-v, facetRay, passing));
    float exit = lerp(flatExit, facetExit, passing);
    float facing = saturate(dot(v, n));
    float fresnel = 0.04 + 0.96 * pow(1.0 - facing, 5.0);
    // Opposite slopes couple very differently to the same internal light.
    // This contrast fills the face instead of outlining its rounded edge.
    float2 bias = v.xz + float2(0.38, -0.21);
    bias *= rsqrt(max(dot(bias, bias), 1e-5));
    float coupling = saturate(0.5 + 1.8 * dot(n.xz, bias));
    float faceGain = lerp(1.0, 0.12 + 1.8 * pow(coupling, 1.3), Contrast * floorMask);
    faceGain *= 1.0 + Variation * floorMask * ((random.z - 0.5) * 0.18 + mottle * 0.7);
    // An insert has one bulb or a few, a banana its three or four in a row:
    // every bulb whose radius is set adds its own halo, each fading with its
    // own spread; the baked glow, one for the whole body, fades with the
    // nearest bulb, and a clear body's face looks onto whichever cavity its
    // ray lands on.
    float4 bulbA = InsertOpticsBulb(Lamp, Position, ray, faceGain, fresnel, exit, response.b, CavitySpread, Spread);
    float4 bulbB = LampB.w > 0.0 ? InsertOpticsBulb(LampB, Position, ray, faceGain, fresnel, exit, response.b, CavitySpread, Spread) : float4(0.0, 0.0, 0.0, 0.0);
    float4 bulbC = LampC.w > 0.0 ? InsertOpticsBulb(LampC, Position, ray, faceGain, fresnel, exit, response.b, CavitySpread, Spread) : float4(0.0, 0.0, 0.0, 0.0);
    float4 bulbD = LampD.w > 0.0 ? InsertOpticsBulb(LampD, Position, ray, faceGain, fresnel, exit, response.b, CavitySpread, Spread) : float4(0.0, 0.0, 0.0, 0.0);
    // Each bulb is driven on its own, the first by the lamp colour and level,
    // the others by a colour with the level in w: its halo carries its own
    // colour, and the baked glow, one for the whole body, is the plain sum of
    // every bulb's light, each reaching as far as its spread lets it, colour
    // and all. So one bulb of a banana lights its end and fades along the
    // body, undimmed by the bulbs beside it that are off, two bulbs on add up
    // where they meet, and a bulb fading in colours every point it reaches in
    // proportion to its level, from the first glimmer on, instead of its
    // colour creeping in from the bulb once it is bright enough to win.
    // One kind of bulb under the insert: an LED kit gives more light than
    // the filaments it replaced, at the same level and colour.
    float output = max(Output, 0.0);
    float3 driveA = max(DriveColor, 0.0) * saturate(Level) * output;
    float3 driveB = max(DriveB.rgb, 0.0) * saturate(DriveB.w) * output;
    float3 driveC = max(DriveC.rgb, 0.0) * saturate(DriveC.w) * output;
    float3 driveD = max(DriveD.rgb, 0.0) * saturate(DriveD.w) * output;
    float3 transmitted = bulbA.x * bulbA.z * driveA + bulbB.x * bulbB.z * driveB + bulbC.x * bulbC.z * driveC + bulbD.x * bulbD.z * driveD;
    float3 image = bulbA.w * bulbA.z * driveA + bulbB.w * bulbB.z * driveB + bulbC.w * bulbC.z * driveC + bulbD.w * bulbD.z * driveD;
    float4 level = float4(max(driveA.r, max(driveA.g, driveA.b)), max(driveB.r, max(driveB.g, driveB.b)),
        max(driveC.r, max(driveC.g, driveC.b)), max(driveD.r, max(driveD.g, driveD.b)));
    float4 reach = float4(bulbA.z, bulbB.z, bulbC.z, bulbD.z);
    float3 glowDrive = reach.x * driveA + reach.y * driveB + reach.z * driveC + reach.w * driveD;
    // The light's colour at unit level, for the tint's hue.
    float3 chroma = glowDrive / max(dot(reach, level), 1e-6);
    // How far the bulbs' colour reaches for the tint: as the light does, full
    // to a tenth of the bulb's own and fading beyond, from a bulb that counts
    // as on from a tenth of full up, so a bulb fading in tints its whole
    // reach at once and grows brighter within it, never creeping outwards
    // from the bulb as the light itself climbs.
    float tinted = dot(saturate(reach * 10.0), saturate(level * 10.0));
    float seen = saturate(bulbA.y + bulbB.y + bulbC.y + bulbD.y);
    float glow = Scatter * response.r * lerp(1.0, faceGain, floorMask * 0.65)
        + EdgeGlow * response.g * (0.25 + 0.75 * response.r);
    // The material sets full-intensity brightness and its emission colour; ROM
    // events only change each bulb's normalized level and RGB through the
    // renderer's property block.
    float3 emission = max(LampColor, 0.0);
    // A rough structure scatters the rays and the bulb's image into the glow.
    float sharp = 1.0 - wear;
    Emission = max(Brightness, 0.0) * emission * (glow * glowDrive * (1.0 + 0.5 * wear) + LampGain * (transmitted + image) * sharp) * finish;
    // Darken the unlit plastic without keeping the bulb or spill light on.
    // Fade back to the authored albedo as the ROM raises lamp intensity.
    // Any bulb that is on lights the plastic, not only the first: a multi-bulb
    // insert with its first bulb off and another on is as bright as with the
    // first on. Only bulbs that exist count.
    float bulbsUp = max(saturate(Level), max(LampB.w > 0.0 ? saturate(DriveB.w) : 0.0,
        max(LampC.w > 0.0 ? saturate(DriveC.w) : 0.0, LampD.w > 0.0 ? saturate(DriveD.w) : 0.0)));
    float bodyBrightness = lerp(saturate(MinBrightness), 1.0, bulbsUp);
    float cavity = 0.35 + 0.65 * response.r;
    float3 tintedBody = Tint * cavity * bodyBrightness;
    // Clear plastic absorbs nothing: an off clear insert shows the bright lamp
    // board or reflector under it, lit by room light that came in through the
    // top, lamp or no lamp. Each face shows the part of that cavity its
    // refracted view ray lands on, as the bulbs found above. The walls and the
    // channel get their share by internal reflection: the baked irradiance.
    float cavityView = lerp(1.0, seen, saturate(CavityContrast));
    // Room light reaches the whole cavity, so its irradiance shapes the clear
    // body less than it darkens the tinted one.
    float3 clearBody = CavityColor * (0.6 + 0.4 * response.r) * lerp(0.6, cavityView, floorMask);
    BaseColor = lerp(tintedBody, Tint * clearBody, saturate(Clarity)) * finish;
    // Moulded plastic is never evenly clear: under room light it is cloudy,
    // blotches of it lighter and darker than the rest, up to two fifths
    // either way at full Variation. The cellular noise averages its
    // neighbours, so it is widened to its full range first. A blotch
    // brightens no further than the albedo's brightest channel can go.
    float mottleGain = 1.0 + Variation * 0.8 * clamp(mottle * 1.4, -0.5, 0.5);
    float bodyPeak = max(BaseColor.r, max(BaseColor.g, BaseColor.b));
    BaseColor *= min(mottleGain, max(1.0, rcp(max(bodyPeak, 1e-5))));
    // Light that came in through the top and went down to a groove does not
    // all leave into the cavity: the facet throws part of it, or under total
    // internal reflection all of it, across to the facet next door, which
    // throws it back up and out through the top, the way a corner reflector
    // returns a beam. So a clear insert in a room shows the room above it in
    // its ribs, brighter from a low view where the top reflects more, and the
    // ribs leaning one way return a different part of the room than those
    // leaning the other, exchanging their brightness as the eye moves round:
    // the movement under the top that tells clear plastic from paint. The
    // path is the view ray refracted in at the top, reflected off the facet
    // the bake found, its mirrored neighbour and the top as the groove's
    // angle sends it, each reflection weighed by the plastic's own Fresnel,
    // then out through the top into the sky's reflection cubemap. What comes
    // back takes the place of as much of the diffuse cavity as it weighs, in
    // the albedo: the room is only as bright as the light that falls on the
    // insert, a directional light, the probes or the sky's own, so the sky
    // gives the room its shape, brighter one way and darker another, and the
    // scene's lighting its brightness, in a dark room as in a bright one.
    float returning = 0.0;
    if (RoomReturn > 0.0 && Clarity > 0.0) {
        float3 facet = normalize(rawNormal + float3(0.0, 1e-5, 0.0));
        facet.y = abs(facet.y);
        // The rib runs radially from the wheel's centre, across its own
        // direction for ribs, round the centre for rings; the neighbouring
        // facet is this one mirrored across the rib, its lean along the rib
        // kept. A moulded facet is never quite flat: a slight bend across it,
        // a degree and a half from one crease to the other, lets a broad
        // highlight sweep within a stripe instead of switching whole.
        float2 radialDir = Position.xz - Starburst.xy;
        radialDir *= rsqrt(max(dot(radialDir, radialDir), 1e-10));
        float2 ribDir = ringed ? float2(-radialDir.y, radialDir.x)
            : ribbed ? normalize(float2(-Starburst.y, Starburst.x) + float2(1e-6, 0.0))
            : gem ? float2(1.0, 0.0) : radialDir;
        float3 along = float3(ribDir.x, 0.0, ribDir.y);
        // The bend dies out at the creases so nothing jumps there.
        float bend = (part - 0.5) * 0.0524 * smoothstep(0.0, 0.25, min(part, 1.0 - part));
        float3 bent = facet * cos(bend) + cross(along, facet) * sin(bend) + along * dot(along, facet) * (1.0 - cos(bend));
        float3 across = float3(-along.z, 0.0, along.x);
        float3 neighbour = normalize(bent - 2.0 * dot(bent, across) * across);
        // The lightmap pass doesn't read the sky; to it the room is the same
        // all round.
#if defined(SHADERPASS) && ((defined(SHADERPASS_LIGHT_TRANSPORT) && (SHADERPASS == SHADERPASS_LIGHT_TRANSPORT)) || (defined(SHADERPASS_META) && (SHADERPASS == SHADERPASS_META)))
        bool readRoom = false;
#else
        bool readRoom = true;
#endif
        // The return switches from one facet's path to its neighbour's at the
        // crease, texel by texel; faded over a couple of screen pixels either
        // side, as the facet coefficients are, the switch no longer stairs.
        float creaseFade = smoothstep(0.0, 2.5 * width, min(part, 1.0 - part));
        float share = saturate(RoomReturn) * saturate(Clarity) * floorMask * flatExit * creaseFade;
        float3 room = float3(0.0, 0.0, 0.0);
        float back = 0.0;
        if (share > 0.0) {
            back = InsertRoomPaths(inside, bent, neighbour, eta, reflectance0, RoomBlur, readRoom, room);
        }
        // The room comes back whether the lamp is on or off, as it does on
        // the real thing: a lit insert still reflects the room above it. But
        // inside a body flooded with the lamp's colour a white reflection
        // reads wrong, so the returned room takes the colour of the bulbs
        // that are on, the more the brighter they are, and a bulb barely on
        // leaves it white rather than glowing coloured. Only bulbs that exist
        // count: a bulb switched off keeps its last drive.
        float lit = max(level.x, max(LampB.w > 0.0 ? level.y : 0.0, max(LampC.w > 0.0 ? level.z : 0.0, LampD.w > 0.0 ? level.w : 0.0)));
        // The bulb colour is first lifted to the luminance of white, capped so
        // a saturated bulb does not blow out, and the tint then runs from
        // white to that: blending first and lifting after kept the white half
        // of a half-made tint and snapped to the colour only at the end.
        float3 lampTint = chroma * min(rcp(max(dot(chroma, float3(0.2126, 0.7152, 0.0722)), 0.05)), 4.0);
        float3 roomTint = lerp(float3(1.0, 1.0, 1.0), lampTint, smoothstep(0.02, 0.3, lit));
        returning = share * back;
        // The lamp's own light takes the same paths: what a facet throws back
        // of the room it throws back of the lamp below, so the returning
        // facets stay lighter than their neighbours however bright the lamp
        // gets, instead of drowning in it, and in the tonemapper's shoulder
        // they go the lighter, paler colour a brighter lamp does.
        Emission *= 1.0 + returning * saturate(lit);
        // An albedo can't pass one, however bright the room is that way.
        BaseColor = saturate(BaseColor * (1.0 - returning) + share * exp2(RoomReturnEV) * room * roomTint);
    }
    // A grey body under a coloured lamp milks its colour. Tinted with the lamp,
    // the body's hue and saturation turn to the light's, the bulbs' colours
    // summed as the glow sums them, as far as their light reaches, its value
    // untouched, so nothing of the lamps stays once they are off.
    float3 untinted = BaseColor;
    BaseColor = InsertOpticsTowardsLampHue(BaseColor, chroma, saturate(LampTint) * saturate(tinted));
    // Turning a grey to a colour keeps its value, not its brightness: a red
    // as light as the grey was has a fifth of its luminance, so the plastic
    // went darker as the lamp came up, where the lamp itself had little to
    // add yet. The room light the plastic scatters does not go away for the
    // lamp colouring it, so the luminance the albedo lost comes back in the
    // albedo, its hue kept and its value raised, as far as the hue lets it:
    // lit by whatever lights the insert, as the returned room is, and lifted
    // as the returned room is.
    // An albedo can be no brighter than its colour allows, a pure red a
    // fifth of a grey, so while the lamp is weak the plastic keeps enough of
    // its grey to hold three fifths of its brightness, pink rather than dark
    // red, and gives it up as the lamp's own glow takes the body over.
    float3 lumWeights = float3(0.2126, 0.7152, 0.0722);
    float lumBefore = dot(untinted, lumWeights);
    float lumAfter = dot(BaseColor, lumWeights);
    float litNow = max(level.x, max(LampB.w > 0.0 ? level.y : 0.0, max(LampC.w > 0.0 ? level.z : 0.0, LampD.w > 0.0 ? level.w : 0.0)));
    // Clear plastic only: a tinted body is its colour already, lit or not.
    float floorLum = 0.6 * lumBefore * (1.0 - smoothstep(0.1, 0.5, litNow)) * saturate(Clarity);
    if (lumAfter < floorLum && lumBefore > lumAfter + 1e-5) {
        BaseColor = lerp(BaseColor, untinted, saturate((floorLum - lumAfter) / (lumBefore - lumAfter)));
    }
    float lost = max(lumBefore - dot(BaseColor, lumWeights), 0.0) * saturate(Clarity) * saturate(RoomReturn);
    if (lost > 0.0) {
        float lum = max(dot(BaseColor, lumWeights), 1e-5);
        float peak = max(BaseColor.r, max(BaseColor.g, BaseColor.b));
        BaseColor *= max(min((lum + lost * exp2(RoomReturnEV)) / lum, rcp(max(peak, 1e-5))), 1.0);
    }
    float4 packedNormal = SAMPLE_TEXTURE2D(SurfaceNormal.tex, SurfaceNormal.samplerstate, UV);
    float3 surface = UnpackNormal(packedNormal);
    float3 shading = normalize(float3(surface.xy * NormalStrength, lerp(1.0, surface.z, saturate(NormalStrength))));
    // Room light enters a clear insert through its flat top, and the facets
    // underneath only redistribute what is already inside. To the lights and
    // reflection probes the surface is that flat top, so the shading normal is
    // flattened towards it, written in the frame the tangent-space normal is
    // read back in. The optical normal that refracts the lamp stays as baked.
    float3 upWS = TransformObjectToWorldNormal(float3(0.0, 1.0, 0.0));
    // The interpolated frame need not be orthonormal, so this is the frame's
    // proper inverse, not three dot products.
    float3 upTS = TransformWorldToTangent(upWS, float3x3(WorldTangent, WorldBitangent, WorldNormal));
    float3 normalTS = lerp(shading, upTS, saturate(Flatten));
    // A facet that returns the room is a mirror inside the plastic to the
    // scene's lights as well: to them it keeps its own relief whatever the
    // flattening, so a spot or the sun glints off it where the sky alone,
    // which carries none of the table's lights, would not. The diffuse stays.
    normalTS = lerp(normalTS, shading, saturate(returning));
    // The fine texture of the moulded plastic, in the reflections: a bump of
    // noise in insert space, as the bake used to put into the normal map,
    // drawn here instead so that it is as fine at any zoom and on any map
    // size, and stays continuous over UV seams. Its slope along the tangent
    // and bitangent, taken as differences a tenth of a cell apart, tilts the
    // shading normal after the flattening, which is the top it sits on. It
    // fades out as the cells shrink below a pixel, before it can shimmer.
    if (SurfaceGrain > 0.0) {
        float3 cellsPerMetre = 1.0 / max(SurfaceGrainSize * 0.001, 1e-5);
        float3 grainCells = Position * cellsPerMetre + Seed * float3(3.7, 9.1, 5.3);
        float3 tangentOS = normalize(TransformWorldToObjectDir(WorldTangent) + float3(1e-6, 0.0, 0.0));
        float3 bitangentOS = normalize(TransformWorldToObjectDir(WorldBitangent) + float3(0.0, 1e-6, 0.0));
        const float step = 0.1;
        float here = InsertSurfaceGrainHeight(grainCells);
        float alongT = InsertSurfaceGrainHeight(grainCells + tangentOS * step);
        float alongB = InsertSurfaceGrainHeight(grainCells + bitangentOS * step);
        float2 slope = float2(alongT - here, alongB - here) / step;
        float visible = InsertDetailVisibility(grainCells);
        normalTS.xy -= slope * (0.3 * saturate(SurfaceGrain) * visible);
    }
    // The flecks' tilt, after the flattening, on the top they are cut into.
    normalTS.xy -= fleckSlope * (0.25 * saturate(Flecks));
    NormalTS = normalize(normalTS + float3(0.0, 0.0, 1e-5));
#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
    // Preserve the authored nits and lamp property blocks. Only the final
    // emission changes units; the resolver supplies UrpPhysicalUnits' scale.
    Emission *= UrpEmissionScale;
#endif
}
#endif
