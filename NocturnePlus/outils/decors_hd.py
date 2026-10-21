"""Nocturne Plus - remasterisation des decors precalcules (640x480, 256 couleurs).

Pour chaque decor BACKDROP\\*.RAW des .POD indiques :
  1. decodage .RAW (indices 8 bits) + .ACT (palette 256 couleurs) -> PNG ;
  2. Real-ESRGAN x4 (supprime la trame des 256 couleurs, reconstruit les details) ;
  3. reduction a 640x480 (Lanczos), contraste local, luminosite calee sur l'original ;
  4. retour sur la palette D'ORIGINE, sans tramage (le .ACT et les .FOG restent valables) ;
  5. ecriture du .RAW dans <sortie>\\BACKDROP\\ (lu a la place de celui du .POD avec le
     correctif "fichiers libres" du lanceur).

Usage : python decors_hd.py <dossier du jeu> <sortie> <realesrgan.exe> POD [POD...]
"""
import os, struct, subprocess, sys, shutil
from PIL import Image, ImageFilter
import numpy as np


def lire_pod(chemin):
    with open(chemin, 'rb') as f:
        d = f.read(96)
        if d[:4] != b'POD2':
            raise ValueError('pas un POD2 : ' + chemin)
        n = struct.unpack_from('<I', d, 88)[0]
        ent = f.read(n * 20)
        entrees = [struct.unpack_from('<IIIII', ent, i * 20) for i in range(n)]
        f.seek(96 + n * 20)
        noms = f.read(max(e[0] for e in entrees) + 256)
    res = {}
    for no, taille, off, _, _ in entrees:
        nom = noms[no:noms.index(b'\0', no)].decode('cp1252')
        res[nom.upper()] = (nom, taille, off)
    return res


def main():
    jeu, sortie, esrgan = sys.argv[1], sys.argv[2], sys.argv[3]
    pods = sys.argv[4:]
    travail = os.path.join(sortie, '_travail')
    dir_in, dir_x4 = os.path.join(travail, 'in'), os.path.join(travail, 'x4')
    for d in (dir_in, dir_x4, os.path.join(sortie, 'BACKDROP')):
        os.makedirs(d, exist_ok=True)

    palettes = {}
    for pod in pods:
        chemin = os.path.join(jeu, pod)
        index = lire_pod(chemin)
        with open(chemin, 'rb') as f:
            for cle, (nom, taille, off) in index.items():
                if not (cle.startswith('BACKDROP\\') and cle.endswith('.RAW') and taille == 640 * 480):
                    continue
                act = index.get(cle[:-4] + '.ACT')
                if act is None:
                    continue
                f.seek(off); raw = f.read(taille)
                f.seek(act[2]); pal = f.read(768)
                base = os.path.basename(nom)[:-4].upper()
                im = Image.frombytes('P', (640, 480), raw); im.putpalette(pal)
                im.convert('RGB').save(os.path.join(dir_in, base + '.png'))
                palettes[base] = (pal, raw)
    print('decors extraits :', len(palettes), flush=True)

    modeles = os.path.join(os.path.dirname(esrgan), 'models')
    subprocess.run([esrgan, '-i', dir_in, '-o', dir_x4, '-n', 'realesrgan-x4plus', '-m', modeles, '-f', 'png'],
                   check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    faits = 0
    for base, (pal, raw_orig) in palettes.items():
        x4 = os.path.join(dir_x4, base + '.png')
        if not os.path.exists(x4):
            print('manquant :', base); continue
        # retour a 640x480, contraste local rendu, puis luminosite/contraste cales sur l'original
        propre = Image.open(x4).convert('RGB').resize((640, 480), Image.LANCZOS)
        propre = propre.filter(ImageFilter.UnsharpMask(radius=1.6, percent=70, threshold=2))
        orig = Image.frombytes('P', (640, 480), raw_orig); orig.putpalette(pal)
        o = np.asarray(orig.convert('RGB')).astype(np.float32)
        x = np.asarray(propre).astype(np.float32)
        x = (x - x.mean((0, 1))) / np.maximum(x.std((0, 1)), 1) * o.std((0, 1)) * 1.05 + o.mean((0, 1))
        propre = Image.fromarray(np.clip(x, 0, 255).astype(np.uint8))
        ref = Image.new('P', (1, 1)); ref.putpalette(pal)
        idx = propre.quantize(palette=ref, dither=Image.Dither.NONE)
        with open(os.path.join(sortie, 'BACKDROP', base + '.RAW'), 'wb') as g:
            g.write(idx.tobytes())
        faits += 1
    print('decors ecrits :', faits, flush=True)
    shutil.rmtree(travail, ignore_errors=True)


if __name__ == '__main__':
    main()
