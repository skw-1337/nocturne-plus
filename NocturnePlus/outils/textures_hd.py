"""Nocturne Plus - textures 3D (objets, personnages) remasterisees et agrandies en 256x256.

Le moteur deduit la taille d'une texture de la longueur de son .RAW (CTextureCache::load :
4096 = 64x64, 16384 = 128x128, 65536 = 256x256) : une texture reecrite en 256x256 est lue telle
quelle, sans correctif. Pour chaque ART\\*.RAW carre des .POD :
  1. decodage .RAW + .ACT (palette) ;
  2. Real-ESRGAN x4, puis reduction a 256x256, contraste local, luminosite calee sur l'original ;
  3. retour sur la palette d'origine sans tramage ;
  4. transparence : sans .OPA, le noir pur est transparent pour le moteur. Le masque d'origine est
     agrandi tel quel (plus proche voisin) et aucun pixel visible ne retombe sur du noir pur ;
  5. le .OPA eventuel est agrandi a la meme taille.
Les images 2D lues avec une taille fixe par le code (icones F0100..F1500, MENUCLD2) sont exclues,
ainsi que les noms presents avec des contenus differents dans plusieurs .POD.

Usage : python textures_hd.py <dossier du jeu> <sortie> <realesrgan.exe>
"""
import os, struct, subprocess, sys, shutil, collections
from PIL import Image, ImageFilter
import numpy as np

TAILLES = {4096: 64, 16384: 128, 65536: 256}
EXCLUS = {'F0100', 'F0300', 'F0500', 'F0700', 'F0900', 'F1100', 'F1300', 'F1500', 'MENUCLD2'}
CIBLE = 256


def lire_pod(chemin):
    with open(chemin, 'rb') as f:
        d = f.read(96)
        if d[:4] != b'POD2':
            raise ValueError('pas un POD2')
        n = struct.unpack_from('<I', d, 88)[0]
        ent = f.read(n * 20)
        entrees = [struct.unpack_from('<IIIII', ent, i * 20) for i in range(n)]
        f.seek(96 + n * 20)
        noms = f.read(max(e[0] for e in entrees) + 256)
    res = {}
    for no, taille, off, _, _ in entrees:
        nom = noms[no:noms.index(b'\0', no)].decode('cp1252')
        res[nom.upper()] = (taille, off)
    return res


def lire(chemin, taille, off):
    with open(chemin, 'rb') as f:
        f.seek(off)
        return f.read(taille)


def main():
    jeu, sortie, esrgan = sys.argv[1], sys.argv[2], sys.argv[3]
    travail = os.path.join(sortie, '_travail_tex')
    dir_in, dir_x4 = os.path.join(travail, 'in'), os.path.join(travail, 'x4')
    for d in (dir_in, dir_x4, os.path.join(sortie, 'ART')):
        os.makedirs(d, exist_ok=True)

    # 1. inventaire de toutes les textures carrees ART\*.RAW avec leur palette
    vus = collections.defaultdict(list)   # base -> [(raw, pal, opa, cote)]
    for p in sorted(os.listdir(jeu)):
        if not p.lower().endswith('.pod'):
            continue
        chemin = os.path.join(jeu, p)
        try:
            index = lire_pod(chemin)
        except Exception:
            continue
        for cle, (taille, off) in index.items():
            if not (cle.startswith('ART\\') and cle.endswith('.RAW') and taille in TAILLES):
                continue
            base = cle[4:-4]
            act = index.get(cle[:-4] + '.ACT')
            if base in EXCLUS or act is None:
                continue
            opa = index.get(cle[:-4] + '.OPA')
            vus[base].append((lire(chemin, taille, off), lire(chemin, 768, act[1]),
                              lire(chemin, *opa) if opa else None, TAILLES[taille]))

    textures = {}
    ignores = 0
    for base, versions in vus.items():
        if any(v[0] != versions[0][0] or v[1] != versions[0][1] for v in versions[1:]):
            ignores += 1   # meme nom, contenus differents selon le .POD : on ne choisit pas
            continue
        textures[base] = versions[0]
    print('textures : %d (ignorees car ambigues : %d)' % (len(textures), ignores), flush=True)

    for base, (raw, pal, opa, cote) in textures.items():
        im = Image.frombytes('P', (cote, cote), raw); im.putpalette(pal)
        im.convert('RGB').save(os.path.join(dir_in, base + '.png'))

    modeles = os.path.join(os.path.dirname(esrgan), 'models')
    subprocess.run([esrgan, '-i', dir_in, '-o', dir_x4, '-n', 'realesrgan-x4plus', '-m', modeles, '-f', 'png'],
                   check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    faits = 0
    for base, (raw, pal, opa, cote) in textures.items():
        x4 = os.path.join(dir_x4, base + '.png')
        if not os.path.exists(x4):
            print('manquant :', base); continue
        palette = np.frombuffer(pal, np.uint8).reshape(256, 3)
        noirs = np.where(palette.sum(1) == 0)[0]
        idx_orig = np.frombuffer(raw, np.uint8).reshape(cote, cote)
        orig_rgb = palette[idx_orig].astype(np.float32)

        propre = Image.open(x4).convert('RGB').resize((CIBLE, CIBLE), Image.LANCZOS)
        propre = propre.filter(ImageFilter.UnsharpMask(radius=1.2, percent=60, threshold=2))
        x = np.asarray(propre).astype(np.float32)
        x = (x - x.mean((0, 1))) / np.maximum(x.std((0, 1)), 1) * orig_rgb.std((0, 1)) + orig_rgb.mean((0, 1))
        propre = Image.fromarray(np.clip(x, 0, 255).astype(np.uint8))

        # palette de remappage sans noir pur : un pixel visible ne doit pas devenir transparent
        pal_sans_noir = palette.copy()
        pal_sans_noir[noirs] = (255, 0, 255)
        ref = Image.new('P', (1, 1)); ref.putpalette(pal_sans_noir.tobytes())
        idx = np.asarray(propre.quantize(palette=ref, dither=Image.Dither.NONE)).copy()

        if opa is None and len(noirs):
            masque = Image.fromarray(np.isin(idx_orig, noirs).astype(np.uint8) * 255)
            masque = np.asarray(masque.resize((CIBLE, CIBLE), Image.NEAREST)) > 127
            idx[masque] = noirs[0]

        with open(os.path.join(sortie, 'ART', base + '.RAW'), 'wb') as g:
            g.write(idx.astype(np.uint8).tobytes())
        if opa is not None and len(opa) == cote * cote:
            a = Image.frombytes('L', (cote, cote), opa).resize((CIBLE, CIBLE), Image.BICUBIC)
            with open(os.path.join(sortie, 'ART', base + '.OPA'), 'wb') as g:
                g.write(a.tobytes())
        faits += 1
    print('textures ecrites :', faits, flush=True)
    shutil.rmtree(travail, ignore_errors=True)


if __name__ == '__main__':
    main()
