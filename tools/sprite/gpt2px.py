from PIL import Image
import numpy as np, sys
from scipy import ndimage as ndi
src,out,N=sys.argv[1],sys.argv[2],int(sys.argv[3]); ncol=int(sys.argv[4])
im=Image.open(src).convert('RGB')
a=np.array(im).astype(float)
S=im.width/N
# mode-ish downscale: median of each block (keeps crisp colours)
H=W=N
res=np.zeros((H,W,3),'uint8')
for y in range(H):
    y0,y1=int(round(y*S)),int(round((y+1)*S))
    for x in range(W):
        x0,x1=int(round(x*S)),int(round((x+1)*S))
        blk=a[y0+1:y1-1,x0+1:x1-1].reshape(-1,3)
        res[y,x]=np.median(blk,axis=0)
# background mask via flood fill from borders
bg=np.array([244,245,245])
diff=np.abs(res.astype(int)-bg).sum(2)
cand=diff<22
lab,n=ndi.label(cand)
border=set(lab[0,:])|set(lab[-1,:])|set(lab[:,0])|set(lab[:,-1]); border.discard(0)
bgm=np.isin(lab,list(border))
mask=~bgm
# keep largest few components, drop specks
lab2,n2=ndi.label(mask,structure=np.ones((3,3)))
sizes=ndi.sum(mask,lab2,range(1,n2+1))
keep=np.isin(lab2,[i+1 for i,s in enumerate(sizes) if s>25])
mask=ndi.binary_fill_holes(keep)
pm=Image.fromarray(res).quantize(ncol,method=Image.MEDIANCUT,dither=Image.NONE)
pal=np.array(pm.getpalette()[:ncol*3]).reshape(-1,3)
def nearest(arr,pal):
    d=((arr.reshape(-1,1,3).astype(int)-pal[None].astype(int))**2).sum(-1); return d.argmin(1),d.min(1)
idx,err=nearest(res,pal)
bad=(err>1800)&mask.reshape(-1)
if bad.sum()>10:
    bp=res.reshape(-1,3)[bad]
    k=Image.fromarray(bp.reshape(-1,1,3).astype('uint8')).quantize(8,method=Image.MEDIANCUT,dither=Image.NONE)
    extra=np.array(k.getpalette()[:24]).reshape(-1,3)
    pal=np.vstack([pal,extra]); idx,err=nearest(res,pal)
qa=pal[idx].reshape(H,W,3).astype('uint8')
# trim light fringe at silhouette
m2=mask.copy()
er=ndi.binary_erosion(mask)
edge=mask&~er
fr=edge&(np.abs(res.astype(int)-bg).sum(2)<70)
m2=mask&~fr
o=np.zeros((H,W,4),'uint8'); o[...,:3]=qa; o[...,3]=np.where(m2,255,0)
# 1px dark outline
OUT=(52,26,34)
ol=ndi.binary_dilation(m2,structure=ndi.generate_binary_structure(2,1))&~m2
o[ol]=OUT+(255,)
img=Image.fromarray(o); bb=img.getbbox(); img=img.crop((bb[0]-1,bb[1]-1,bb[2]+1,bb[3]+1)); img.save(out); print(img.size,len(pal))
