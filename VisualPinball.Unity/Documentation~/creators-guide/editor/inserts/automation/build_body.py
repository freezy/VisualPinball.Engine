"""Builds stills/body.json from the images body-material.md shows, one still per image, named m-<image>.

A new parameter needs its image name prefix mapped to its shader property in `floats` or `colors` below;
the value is read from the image name (e.g. clarity-0.5 -> _InsertClarity=0.5). Run after editing the page.
"""
import json, re
from paths import DOCS, HERE

# L35, centred a little off its middle, from 6 cm above: L35 lit, L34 unlit above it, both in frame.
VIEW = {'camOffset': [0.00298807025, 0.0600357875, -0.00228381157], 'lookOffset': [0.00298807025, 3.579026e-05, 0.0007161796]}
# IOR shows on the refraction of the ribs, which reads best at an angle.
IOR_VIEW = {'camOffset': [0.02, 0.045, -0.02], 'lookOffset': [0, 0, 0], 'frameBoth': False}
# The room comes back off shallow facets only at an angle, and is lost on a lit insert: these rows swap the pair, L34 lit
# and L35 off, and look down at L35 from the user's Scene view, 59 degrees up from the right, at 0.65 of its distance.
# The surface normal map shows from there too, where the ribs catch the light; from above they look alike.
ROOM_ROWS = ('room-', 'surface-normal-strength-', 'flecks-', 'fleck-size-')
# The flecks are fine: along the same line, a little closer, at 0.55 of the Scene view's distance.
FLECK_VIEW = {'camOffset': [0.01763, 0.03152, -0.00043], 'lookOffset': [0.00334, 0, -0.01311], 'frameBoth': True}
ROOM_VIEW = {'camOffset': [0.02023, 0.03726, 0.00189], 'lookOffset': [0.00334, 0, -0.01311], 'frameBoth': True}

floats = {
	'clarity': '_InsertClarity', 'cavity-spread': '_InsertCavitySpread', 'cavity-contrast': '_InsertCavityContrast',
	'room-return-exposure': '_InsertRoomReturnEV', 'room-return': '_InsertRoomReturn', 'room-blur': '_InsertRoomBlur',
	'smoothness': '_Smoothness', 'surface-normal-strength': '_NormalStrength', 'facet-flatten': '_InsertFacetFlatten',
	'surface-grain-size': '_SurfaceGrainSize', 'surface-grain': '_SurfaceGrain', 'grain': '_OpticalGrain',
	'fleck-size': '_OpticalFleckSize', 'flecks': '_OpticalFlecks', 'max-brightness': '_InsertLuminosity',
	'strength': '_OpticalLampGain', 'ior': '_OpticalIor', 'lamp-spread': '_OpticalSpread', 'baked-scatter': '_OpticalScatter',
	'edge-glow': '_OpticalEdgeGlow', 'roughness': '_InsertRoughness', 'facet-contrast': '_OpticalContrast',
	'mottle-amount': '_OpticalVariation', 'mottle-size': '_OpticalMottleSize', 'variation-seed': '_OpticalSeed',
}
colors = {
	'tint-white': '_BaseColor=1,1,1', 'tint-red': '_BaseColor=1,0.3,0.3', 'tint-blue': '_BaseColor=0.4,0.6,1',
	'cavity-color-dark': '_InsertCavityColor=0.3,0.3,0.3', 'cavity-color-cool': '_InsertCavityColor=0.7,0.76,0.86',
	'cavity-color-warm': '_InsertCavityColor=0.9,0.8,0.55',
	# The neutral emission is the material's own; quarter is a fourth of it; warm keeps a warm white's proportions at its brightness.
	'lamp-emission-quarter': '_EmissiveColor=341.78,341.78,341.78', 'lamp-emission-neutral': '_EmissiveColor=1367.12,1367.12,1367.12',
	'lamp-emission-warm': '_EmissiveColor=1367.12,884.6,482.5',
}
# What a value needs to show at all.
extra = {
	'surface-grain-size': ['_SurfaceGrain=1'],
	# At the clear material's Min Brightness (0.485) the tinted and the clear body come out alike; tinted plastic shows dark when off.
	'clarity': ['_InsertMinBrightness=0.1'],
	# At the clear material's Cavity Spread (2.5) nearly every face sees the cavity, and the contrast has little to darken.
	'cavity-contrast': ['_InsertCavitySpread=1'],
	# The faceted body throws a light's highlight off its ribs: flat, it mirrors one on the unlit insert (no cover, below).
	'smoothness': ['_InsertFacetFlatten=0.95'],
	# The baked glow (1.2) drowns the light the facets pass: lowered, the rays and their contrast show.
	'roughness': ['_OpticalScatter=0.4'],
	'facet-contrast': ['_OpticalScatter=0.4'],
	# At the material's 0.2 mm the mottle is a fine grain lost among the ribs' own: at 1 mm it shows as blotches.
	'mottle-amount': ['_OpticalMottleSize=1'],
	'variation-seed': ['_OpticalMottleSize=1'],
}

text = (DOCS / 'body-material.md').read_text(encoding='utf-8')
names = re.findall(r'src="body-material/([^"]+)\.png"', text)
shots, skipped = [], []
for name in names:
	# Inspector crops come from the material shots; Min Brightness needs a red insert: stills/min-brightness.json.
	if name.startswith('inspector-') or name.startswith('min-brightness'):
		skipped.append(name)
		continue
	shot = {'name': 'm-' + name}
	shot.update(IOR_VIEW if name.startswith('ior-') else FLECK_VIEW if name.startswith(('flecks-', 'fleck-size-')) else ROOM_VIEW if name.startswith(ROOM_ROWS) else VIEW)
	if name in colors:
		shot['colors'] = [colors[name]]
	elif name.startswith('radius-'):
		# The first virtual bulb, 21.5 mm under the insert's origin, at the radius in the name.
		r = name[len('radius-'):]
		shot['lamps'] = [f'0,-0.0215305761,0,{r}', '0,0,0,0', '0,0,0,0', '0,0,0,0']
	else:
		key = next(k for k in sorted(floats, key=len, reverse=True) if name.startswith(k + '-'))
		value = name[len(key) + 1:]
		shot['floats'] = [f'{floats[key]}={value}'] + extra.get(key, [])
	# The body's look doesn't depend on the cover: no haze over it, only its reflection; none at all where its own
	# reflection is the point.
	if name.startswith('smoothness-'):
		shot['noCover'] = True
	else:
		shot['coverFloats'] = ['_CoverOpacity=0']
	shots.append(shot)

# L33 above and L32 beyond are in view and on in the scene: kept off, so only L35 is lit.
room = [shot for shot in shots if shot['name'][2:].startswith(ROOM_ROWS)]
shots = [shot for shot in shots if shot not in room]
config = {'insert': 'Starburst - L35', 'unlit': 'Starburst - L34', 'off': ['Starburst - L33', 'Starburst - L32'], 'shots': shots}
json.dump(config, open(HERE / 'stills' / 'body.json', 'w'), indent=1)
json.dump(dict(config, insert='Starburst - L34', unlit='Starburst - L35', shots=room), open(HERE / 'stills' / 'body-room.json', 'w'), indent=1)
print(len(shots), 'shots,', len(room), 'room shots; skipped', skipped)
