"""Preview84/85: extract MC block appearance from the local client jar for Wukong-drawn (native) blocks.

Output (little-endian) for NativeBlockMesh.cs:
  b"DQJBT1\\0\\0", u32 textureCount, textureCount * 1024 bytes RGBA (row 0 = top),
  u32 blockCount, per block: u8 idLength, id (utf-8, e.g. minecraft:stone), u8 kind, then
    kind 0 cube, 1 column with axis, 2 slab, 3 stairs: 6 * u16 texture index (down, up, north, south, west, east);
    kind 4 small prop baked from its model (Preview85): u16 quadCount, per quad 12 f32 corners in block space
      (0..1, counter-clockwise seen from the front), 3 f32 normal, 4 u8 RGBA.
Biome tints are baked (plains grass/foliage). Translucent textures, multipart models, rotated face UVs and
blocks whose look depends on state properties (other than the handled kinds) are left out: MC keeps drawing them.
Only reads the jar. Usage: extract_block_textures.py CLIENT_JAR OUT_FILE [REPORT_JSON]"""
import io, json, math, struct, sys, zipfile
from PIL import Image

FACES = ('down', 'up', 'north', 'south', 'west', 'east')
DIR = {'down': (0, -1, 0), 'up': (0, 1, 0), 'north': (0, 0, -1), 'south': (0, 0, 1), 'west': (-1, 0, 0), 'east': (1, 0, 0)}
GRASS = (0x91, 0xBD, 0x59)
FOLIAGE = {'birch': (0x80, 0xA7, 0x55), 'spruce': (0x61, 0x99, 0x61), 'mangrove': (0x8D, 0xB1, 0x27), 'cherry': None, 'azalea': None, 'flowering_azalea': None}
DEFAULT_FOLIAGE = (0x77, 0xAB, 0x2F)
KIND_PARENTS = (('block/slab', 2), ('block/slab_top', 2), ('block/stairs', 3), ('block/cube_column_horizontal', 1), ('block/cube_column', 1))

def strip(name):
    return name.split(':', 1)[-1]

def face_point(face, f, T, s, t):
    """(s, t) in [0,1] along MC's default face UV directions -> point on the element face (block units)."""
    if face == 'up': return (f[0] + s * (T[0] - f[0]), T[1], f[2] + t * (T[2] - f[2]))
    if face == 'down': return (f[0] + s * (T[0] - f[0]), f[1], T[2] - t * (T[2] - f[2]))
    if face == 'north': return (T[0] - s * (T[0] - f[0]), T[1] - t * (T[1] - f[1]), f[2])
    if face == 'south': return (f[0] + s * (T[0] - f[0]), T[1] - t * (T[1] - f[1]), T[2])
    if face == 'west': return (f[0], T[1] - t * (T[1] - f[1]), f[2] + s * (T[2] - f[2]))
    return (T[0], T[1] - t * (T[1] - f[1]), T[2] - s * (T[2] - f[2]))

def default_uv(face, f, T):
    """MC BlockElement default face UVs, element bounds in 0..16."""
    return {'up': [f[0], f[2], T[0], T[2]], 'down': [f[0], 16 - T[2], T[0], 16 - f[2]],
            'north': [16 - T[0], 16 - T[1], 16 - f[0], 16 - f[1]], 'south': [f[0], 16 - T[1], T[0], 16 - f[1]],
            'west': [f[2], 16 - T[1], T[2], 16 - f[1]], 'east': [16 - T[2], 16 - T[1], 16 - f[2], 16 - f[1]]}[face]

def rotate(p, rot, rescale=True):
    if not rot: return p
    o = [c / 16 for c in rot.get('origin', [8, 8, 8])]; a = math.radians(rot.get('angle', 0)); axis = rot.get('axis', 'y')
    v = [p[0] - o[0], p[1] - o[1], p[2] - o[2]]; c, s = math.cos(a), math.sin(a)
    if axis == 'x': v = [v[0], v[1] * c - v[2] * s, v[1] * s + v[2] * c]
    elif axis == 'y': v = [v[0] * c + v[2] * s, v[1], -v[0] * s + v[2] * c]
    else: v = [v[0] * c - v[1] * s, v[0] * s + v[1] * c, v[2]]
    if rescale and rot.get('rescale') and abs(c) > 1e-6:
        v = [v[i] / c if 'xyz'[i] != axis else v[i] for i in range(3)]
    return (v[0] + o[0], v[1] + o[1], v[2] + o[2])

def rotate_dir(n, rot):
    if not rot: return n
    q = rotate((n[0] + .5, n[1] + .5, n[2] + .5), dict(rot, origin=[8, 8, 8]), rescale=False)
    v = (q[0] - .5, q[1] - .5, q[2] - .5); l = math.sqrt(sum(c * c for c in v)) or 1
    return tuple(c / l for c in v)

def main(jar_path, out_path, report_path=None):
    jar = zipfile.ZipFile(jar_path)
    names = set(jar.namelist())
    models = {}
    def model(name):
        name = strip(name)
        if name in models: return models[name]
        path = 'assets/minecraft/models/' + name + '.json'
        models[name] = json.loads(jar.read(path)) if path in names else None
        return models[name]
    def chain(name):
        out = []
        while name and len(out) < 16:
            m = model(name)
            if m is None: break
            out.append((strip(name), m)); name = m.get('parent')
        return out
    pixels = {}
    def image_px(ref, tint):
        key = (ref, tint)
        if key in pixels: return pixels[key]
        path = 'assets/minecraft/textures/' + strip(ref) + '.png'; px = None
        if path in names:
            img = Image.open(io.BytesIO(jar.read(path))).convert('RGBA')
            if img.width == 16 and img.height >= 16 and img.height % 16 == 0:
                px = [img.getpixel((x, y)) for y in range(16) for x in range(16)]  # animated strips: first frame
                if sum(1 for p in px if 0 < p[3] < 250) > 8: px = None  # translucent: MC keeps drawing it
                elif tint: px = [(p[0] * tint[0] // 255, p[1] * tint[1] // 255, p[2] * tint[2] // 255, p[3]) for p in px]
        pixels[key] = px; return px
    textures = []; tex_index = {}
    def texture(ref, tint):
        key = (ref, tint)
        if key in tex_index: return tex_index[key]
        px = image_px(ref, tint)
        if px is None: return None
        tex_index[key] = len(textures); textures.append(px); return tex_index[key]
    def over(base, top):
        out = []
        for b, t in zip(textures[base], textures[top]):
            a = t[3] / 255
            out.append((round(b[0] * (1 - a) + t[0] * a), round(b[1] * (1 - a) + t[1] * a), round(b[2] * (1 - a) + t[2] * a), max(b[3], t[3])))
        textures.append(out); return len(textures) - 1
    def bake(elements, resolve, tint):
        """Model elements -> coloured texel quads; None if a face is rotated or a texture is unusable."""
        quads = []
        for e in elements:
            f = [c / 16 for c in e['from']]; T = [c / 16 for c in e['to']]; rot = e.get('rotation')
            for face, spec in e.get('faces', {}).items():
                if spec.get('rotation', 0) or face not in DIR: return None
                ref = resolve(spec['texture'])
                px = image_px(ref, tint if 'tintindex' in spec else None) if ref else None
                if px is None: return None
                a0, b0, a1, b1 = spec.get('uv') or default_uv(face, e['from'], e['to'])
                if a1 == a0 or b1 == b0: continue
                normal = rotate_dir(DIR[face], rot)
                for py in range(16):
                    for pu in range(16):
                        c = px[py * 16 + pu]
                        if c[3] < 128: continue
                        s0, s1 = sorted(((pu - a0) / (a1 - a0), (pu + 1 - a0) / (a1 - a0)))
                        t0, t1 = sorted(((py - b0) / (b1 - b0), (py + 1 - b0) / (b1 - b0)))
                        s0, s1, t0, t1 = max(0.0, s0), min(1.0, s1), max(0.0, t0), min(1.0, t1)
                        if s1 <= s0 or t1 <= t0: continue
                        corners = [rotate(face_point(face, f, T, s, t), rot) for s, t in ((s0, t0), (s1, t0), (s1, t1), (s0, t1))]
                        ax = [corners[1][i] - corners[0][i] for i in range(3)]; bx = [corners[2][i] - corners[0][i] for i in range(3)]
                        cr = (ax[1] * bx[2] - ax[2] * bx[1], ax[2] * bx[0] - ax[0] * bx[2], ax[0] * bx[1] - ax[1] * bx[0])
                        if sum(cr[i] * normal[i] for i in range(3)) < 0: corners.reverse()
                        quads.append((corners, normal, (c[0], c[1], c[2], 255)))
        return quads if 0 < len(quads) <= 4096 else None
    blocks = []; props = []; skipped = {}
    for path in sorted(n for n in names if n.startswith('assets/minecraft/blockstates/') and n.endswith('.json')):
        block = path.rsplit('/', 1)[1][:-5]
        state = json.loads(jar.read(path))
        variants = state.get('variants')
        if not variants: skipped[block] = 'multipart'; continue
        pick = None
        for key in ('', 'type=bottom', 'axis=y', 'facing=east,half=bottom,shape=straight'):
            if key in variants: pick = variants[key]; break
        if pick is None: pick = next(iter(variants.values()))
        if isinstance(pick, list): pick = pick[0]
        parents = chain(pick['model'])
        if not parents: skipped[block] = 'no-model'; continue
        names_chain = [n for n, _ in parents]
        kind = 0
        for parent, k in KIND_PARENTS:
            if parent in names_chain: kind = k; break
        tex = {}; elements = None
        for _, m in reversed(parents): tex.update(m.get('textures', {}))
        for _, m in parents:
            if 'elements' in m: elements = m['elements']; break
        if not elements: skipped[block] = 'no-elements'; continue
        def resolve(ref):
            for _ in range(8):
                if not ref.startswith('#'): return ref
                ref = tex.get(ref[1:], '')
            return None
        tint = FOLIAGE.get(block.replace('_leaves', ''), DEFAULT_FOLIAGE) if 'leaves' in block else GRASS
        if kind == 0 and {(tuple(e['from']), tuple(e['to'])) for e in elements} != {((0, 0, 0), (16, 16, 16))}:
            # Small props: only blocks whose look does not depend on state (one variant), and no random model pick.
            quads = bake(elements, resolve, tint) if len(variants) == 1 and not isinstance(next(iter(variants.values())), list) else None
            if quads: props.append(('minecraft:' + block, quads))
            else: skipped[block] = 'not-cube'
            continue
        faces = []
        for face in FACES:
            index = None
            for e in elements:
                f = e.get('faces', {}).get(face)
                if not f: continue
                ref = resolve(f['texture'])
                if not ref: continue
                t = texture(ref, tint if 'tintindex' in f else None)
                if t is None: index = None; break
                index = t if index is None else over(index, t)  # grass side overlay
            if index is None: break
            faces.append(index)
        if len(faces) != 6: skipped[block] = 'texture'; continue
        blocks.append(('minecraft:' + block, kind, faces))
    records = blocks + [(b, 4, q) for b, q in props]
    with open(out_path, 'wb') as out:
        out.write(b'DQJBT1\0\0'); out.write(struct.pack('<I', len(textures)))
        for px in textures: out.write(bytes(c for p in px for c in p))
        out.write(struct.pack('<I', len(records)))
        for block_id, kind, data in records:
            raw = block_id.encode('utf-8'); out.write(struct.pack('<B', len(raw))); out.write(raw)
            if kind < 4: out.write(struct.pack('<B6H', kind, *data)); continue
            out.write(struct.pack('<BH', 4, len(data)))
            for corners, normal, rgba in data:
                out.write(struct.pack('<15f', *[c for p in corners for c in p], *normal)); out.write(bytes(rgba))
    kinds = {k: sum(1 for b in blocks if b[1] == k) for k in range(4)}; kinds[4] = len(props)
    report = dict(blocks=len(records), textures=len(textures), kinds=kinds, skipped=len(skipped))
    if report_path:
        json.dump(dict(report, skippedReasons=skipped, included=[b[0] for b in blocks], props=[b[0] for b in props]),
                  open(report_path, 'w', encoding='utf-8'), indent=1)
    print(json.dumps(report))

if __name__ == '__main__':
    main(*sys.argv[1:])
