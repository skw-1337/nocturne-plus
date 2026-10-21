"""Nocturne Plus - icone : feutre de Stranger en silhouette devant une lune pale, fond sang seche.
Genere src\\nocturne-plus.ico (16 a 256 px) et un apercu PNG.

Usage : python icone.py <dossier src>
"""
import os, sys
from PIL import Image, ImageDraw, ImageFilter

S = 1024  # dessin en grand puis reduction (bords lisses)


def dessiner():
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))

    # fond : carre arrondi, degrade vertical nuit -> sang seche
    fond = Image.new('RGBA', (S, S))
    d = ImageDraw.Draw(fond)
    for y in range(S):
        t = y / S
        d.line([(0, y), (S, y)], fill=(int(14 + 52 * t), int(9 + 3 * t), int(12 + 6 * t), 255))
    masque = Image.new('L', (S, S), 0)
    ImageDraw.Draw(masque).rounded_rectangle([24, 24, S - 24, S - 24], radius=190, fill=255)
    im.paste(fond, (0, 0), masque)

    # lune pale avec halo
    halo = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(halo).ellipse([300, 120, 820, 640], fill=(222, 210, 190, 90))
    halo = halo.filter(ImageFilter.GaussianBlur(60))
    im = Image.alpha_composite(im, Image.composite(halo, Image.new('RGBA', (S, S)), masque))
    lune = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(lune).ellipse([340, 160, 780, 600], fill=(226, 214, 192, 255))
    im = Image.alpha_composite(im, lune)

    # feutre (fedora) en silhouette, centre bas
    c = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    dc = ImageDraw.Draw(c)
    noir = (8, 6, 7, 255)
    # bord : ellipse large, releve sur les cotes
    dc.ellipse([110, 630, 914, 790], fill=noir)
    dc.ellipse([104, 612, 300, 724], fill=noir)
    dc.ellipse([724, 612, 920, 724], fill=noir)
    # calotte : evasee vers le bas, arrondie en haut
    dc.polygon([(262, 700), (300, 470), (360, 380), (512, 352), (664, 380), (724, 470), (762, 700)], fill=noir)
    dc.ellipse([330, 330, 694, 470], fill=noir)
    c = c.filter(ImageFilter.GaussianBlur(1.5))
    im = Image.alpha_composite(im, c)
    dr = ImageDraw.Draw(im)
    # pli du dessus et reflets lunaires sur le feutre
    dr.line([(512, 372), (512, 470)], fill=(46, 38, 40, 255), width=12)
    dr.arc([330, 330, 694, 470], 200, 340, fill=(70, 60, 58, 255), width=8)
    # ruban rouge sang
    dr.polygon([(272, 628), (752, 628), (758, 690), (266, 690)], fill=(176, 22, 34, 255))
    dr.line([(272, 632), (752, 632)], fill=(214, 60, 64, 255), width=6)
    # lisere rouge
    ImageDraw.Draw(im).rounded_rectangle([24, 24, S - 24, S - 24], radius=190, outline=(150, 18, 28, 255), width=14)
    return im


def main():
    src = sys.argv[1]
    im = dessiner()
    tailles = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (16, 16)]
    im.resize((256, 256), Image.LANCZOS).save(os.path.join(src, 'nocturne-plus.ico'), sizes=tailles)
    apercu = Image.new('RGBA', (256 + 16 + 64 + 16 + 32 + 16 + 16, 256), (24, 20, 21, 255))
    x = 0
    for t in (256, 64, 32, 16):
        apercu.alpha_composite(im.resize((t, t), Image.LANCZOS), (x, 256 - t)); x += t + 16
    apercu.save(os.path.join(src, 'nocturne-plus-apercu.png'))


if __name__ == '__main__':
    main()
