import numpy as np, json, os, math, subprocess
from PIL import Image
from scipy import ndimage as ndi
S=0.53; OUT=(52,26,34)
names=['fairy_red','fairy_blue','fairy_green','ghost','ufo','umbrella']
a=np.array(Image.open(r'E:\Unity\BS\ArtSource\Reference\Touhou_Mobs_6_Idle_Transparent_256Grid.png').convert('RGBA'))
cells=[]
for i,n in enumerate(names):
    r,c=divmod(i,3); cell=a[r*256:(r+1)*256,c*256:(c+1)*256]
    ys,xs=np.where(cell[...,3]>=40); cell=cell[ys.min():ys.max()+1,xs.min():xs.max()+1]
    h,w=cell.shape[:2]; nh,nw=round(h*S),round(w*S)
    f=cell.astype(float); pm=f[...,:3]*(f[...,3:4]/255)
    ch=[np.array(Image.fromarray((pm[...,k] if k<3 else f[...,3]).astype('float32'),'F').resize((nw,nh),Image.BOX)) for k in range(4)]
    al=ch[3]; rgb=np.clip(np.dstack(ch[:3])/np.maximum(al[...,None]/255,1e-3),0,255)
    cells.append((rgb,al))
allp=np.concatenate([r[al>=160].reshape(-1,3) for r,al in cells]).astype('uint8')
pm=Image.fromarray(allp.reshape(-1,1,3)).quantize(48,method=Image.MEDIANCUT,dither=Image.NONE)
pal=np.array(pm.getpalette()[:48*3]).reshape(-1,3)
base={}
for n,(rgb,al) in zip(names,cells):
    h,w=al.shape; solid=al>=150
    lab,k=ndi.label(solid,structure=np.ones((3,3))); sz=ndi.sum(solid,lab,range(1,k+1)); solid=np.isin(lab,[i+1 for i,s in enumerate(sz) if s>=8])
    d=((rgb.reshape(-1,1,3).astype(int)-pal[None].astype(int))**2).sum(-1).argmin(1)
    o=np.zeros((h,w,4),'uint8'); o[...,:3]=pal[d].reshape(h,w,3); o[...,3]=np.where(solid,255,0)
    ol=ndi.binary_dilation(solid,structure=ndi.generate_binary_structure(2,1))&~solid; o[ol]=OUT+(255,)
    base[n]=o
A="E:/SteamLibrary/steamapps/common/Aseprite/Aseprite.exe"
def lua(v):
    if isinstance(v,dict): return "{"+",".join(f'{"end_" if k=="end" else k}={lua(x)}' for k,x in v.items())+"}"
    if isinstance(v,list): return "{"+",".join(lua(x) for x in v)+"}"
    if isinstance(v,str): return json.dumps(v)
    return str(v)
for n in names:
    b=base[n]; h,w=b.shape[:2]; CW,CH=w+8,h+12
    os.makedirs(f'mob/{n}',exist_ok=True)
    N=6; man=dict(w=CW,h=CH,frames=[],tags=[dict(name='idle',start=1,end=N),dict(name='move',start=N+1,end=2*N)])
    for t in range(2*N):
        p=2*math.pi*(t%N)/N; amp=2 if t<N else 3
        dy=round(amp*math.sin(p)); sh=round((1 if t<N else 2)*math.sin(p-1.0))
        img=np.zeros((CH,CW,4),'uint8'); sq=1 if (t>=N and n.startswith('fairy') and math.sin(p)<-0.3) else 0
        for y in range(h):
            x0=4+round(sh*(1-y/h)); yy=6+y+dy
            if 0<=yy<CH: img[yy,x0:x0+w]=b[y]
        if sq: img[:,:,:]=np.roll(img,0,axis=0)
        paths={}
        for ln in ('fx_back','body','fx_front'):
            pth=f'mob/{n}/{t+1:02d}_{ln}.png'; Image.fromarray(img if ln=='body' else np.zeros_like(img)).save(pth); paths[ln]=os.path.abspath(pth).replace(chr(92),'/')
        man['frames'].append(dict(dur=110 if t<N else 80,layers=paths))
    open(f'mob/{n}.lua','w').write('return '+lua(man))
    D=rf'E:\Unity\BS\ArtSource\Mobs\{n}'; os.makedirs(D+r'\Exports',exist_ok=True)
    subprocess.run([A,'-b','--script-param',f'manifest={os.path.abspath(f"mob/{n}.lua")}','--script-param',f'out={D}\{n}_anim.aseprite','--script','build.lua'],capture_output=True)
    subprocess.run([A,'-b',f'{D}\{n}_anim.aseprite','--sheet',f'{D}\Exports\{n}_sheet.png','--data',f'{D}\Exports\{n}_sheet.json','--format','json-array','--list-tags','--sheet-type','rows','--sheet-columns','12'],capture_output=True)
    Image.fromarray(b).save(D+f'\Exports\{n}.png'); print(n,CW,CH,h)
