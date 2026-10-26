import sys
import numpy as np
from PIL import Image
from scipy import ndimage as ndi
src,out=sys.argv[1],sys.argv[2]
a=np.array(Image.open(src).convert('RGBA')).astype(float)
# composite the baked translucent haze over black so the sheet becomes a plain RGB picture
rgb=a[...,:3]*(a[...,3:4]/255.0)
H,W=rgb.shape[:2]
lum=rgb.mean(2); mx=rgb.max(2); mn=rgb.min(2); sat=(mx-mn)
fg=(lum>95)|(sat>60)
fg=ndi.binary_dilation(fg,iterations=2)
w=(~fg).astype(float)
sig=40
bgm=np.stack([ndi.gaussian_filter(rgb[...,c]*w,sig)/np.maximum(ndi.gaussian_filter(w,sig),1e-3) for c in range(3)],-1)
d=np.abs(rgb-bgm).sum(2)
T=float(sys.argv[3]) if len(sys.argv)>3 else 70
strong=ndi.binary_opening(d>T,iterations=1)
lab,n=ndi.label(strong,structure=np.ones((3,3)))
sizes=ndi.sum(strong,lab,range(1,n+1))
strong=np.isin(lab,[i+1 for i,s_ in enumerate(sizes) if s_>40])
weak=d>T*0.4
m=strong|(weak&ndi.binary_dilation(strong,iterations=4))
m=ndi.binary_closing(m,iterations=3)
filled=ndi.binary_fill_holes(m)
hl,hn=ndi.label(filled&~m,structure=np.ones((3,3)))
if hn:
    hs=ndi.sum(np.ones_like(hl),hl,range(1,hn+1))
    m=m|np.isin(hl,[i+1 for i,v in enumerate(hs) if v<900])
m=ndi.binary_opening(m,iterations=1)
m=ndi.binary_erosion(m,iterations=1)
o=np.zeros((H,W,4),'uint8'); o[...,:3]=np.clip(rgb,0,255); o[...,3]=np.where(m,255,0)
Image.fromarray(o).save(out)
v=Image.new('RGBA',(W,H),(255,255,255,255)); v.alpha_composite(Image.fromarray(o)); v.convert('RGB').save(out.replace('.png','_white.png'))
print(m.mean())
