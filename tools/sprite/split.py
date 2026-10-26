from PIL import Image
import numpy as np
from scipy import ndimage as ndi
im=np.array(Image.open('gpt_n196.png').convert('RGBA'))
H,W=im.shape[:2]; a=im[...,3]>0
yy,xx=np.mgrid[0:H,0:W]
line=79-(52-xx)*0.49
pole=(np.abs(yy-line)<=3.2)&(xx>=44)&(xx<=55)
M=a&(yy>=58)&(((xx<=54)&((xx<42)|(yy>=70)))&~((xx>=44)&(yy>=66)&(yy<=106)&~pole)|pole&(xx<=54))
M&=(xx<=54)
lab,n=ndi.label(M,structure=np.ones((3,3)))
sizes=ndi.sum(M,lab,range(1,n+1))
g=np.isin(lab,[i+1 for i,s in enumerate(sizes) if s>40])
np.save('gmask.npy',g)
out=im.copy(); out[g]=[0,255,255,255]
bg=Image.new('RGBA',(W,H),(120,160,120,255)); bg.alpha_composite(Image.fromarray(out))
bg.resize((W*5,H*5),Image.NEAREST).crop((0,250,420,700)).save('gmask.png')
