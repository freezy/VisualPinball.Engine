"""Mattes the showcase renders (Pinball/Automation/Showcase/Shoot Lit and Shoot Gizmo in LookDev~) into inserts-showcase-lit.png and inserts-showcase-unlit.png.

Each is rendered on black and on white; alpha comes from the difference. Previews over grey and white go to WORK.
"""
import numpy as np
from PIL import Image
from paths import DOCS, LOOKDEV, WORK, output
S=str(output(LOOKDEV, 'showcase'))+'/'
D=str(DOCS)+'/'
def load(n): return np.asarray(Image.open(S+n+'.png').convert('RGB')).astype(np.float32)
def save(rgba, dst, thresh, margin):
    rows=np.where((rgba[...,3]>thresh).any(axis=1))[0]
    y0=max(rows[0]-margin,0); y1=min(rows[-1]+margin+1,rgba.shape[0])
    out=Image.fromarray(rgba[y0:y1],'RGBA'); out.save(D+dst+'.png', optimize=True)
    print(dst,out.size,'rows',y0,y1)
    for name,col in [('grey',(128,128,128,255)),('white',(255,255,255,255))]:
        bg=Image.new('RGBA',out.size,col); bg.alpha_composite(out)
        bg.convert('RGB').resize((1600,round(out.height*1600/out.width))).save(WORK/('showcase-'+dst+'-'+name+'.png'))
# lit: the white background comes out tonemapped, and the bloom adds a red veil over everything
b,w=load('lit-black'),load('lit-white')
wref=w[5,5]; floor=float(b[:40].max(axis=2).min())
geom=np.clip((1-((w-b)/wref).mean(axis=2)-0.16)/0.84,0,1)
glow=np.clip((b.max(axis=2)-30)/(255-30),0,1)
a=np.maximum(geom,glow)
c=np.clip(b/np.maximum(a[...,None],1e-3),0,255)
save(np.dstack([c,a*255]).round().astype(np.uint8),'inserts-showcase-lit',2,24)
# outlines: grey, the alpha from the matte
b,w=load('gizmo-black'),load('gizmo-white')
a=np.clip(1-((w-b)/w[5,5]).mean(axis=2),0,1)
a[a<0.02]=0
rgba=np.dstack([np.full(a.shape+(3,),128,np.float32),a*255]).round().astype(np.uint8)
save(rgba,'inserts-showcase-unlit',0,33)
