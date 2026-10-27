import json,sys
import numpy as np
from PIL import Image
from scipy import ndimage as ndi
rows={'reimu2':['skilla','skillb'],'marisa':['qa','qb','ea'],'sakuya':['qa','e1','e2','e3','r1','r2','r3'],'sanae':['q1','q2','q3','e1','e2','e3','r1','r2']}
used={'reimu2':[('skilla',range(5)),('skillb',range(3))],'marisa':[('skill',range(8)),('ea',range(6))],'sakuya':[('qa',[0]),('e1',range(7)),('e2',range(4)),('e3',range(4)),('r1',range(7)),('r2',range(5)),('r3',range(3))],'sanae':[('q1',range(8)),('q2',range(5)),('e1',range(6)),('e2',range(4)),('e3',range(3)),('r1',range(7)),('r2',range(5))]}
def straight(a):
    """longest run of rows where a column edge is a hard vertical cut (opaque next to empty) and same for horizontal"""
    best=0
    for arr in (a,a.T):
        h,w=arr.shape
        for x in range(1,w-1):
            left=arr[:,x-1];cur=arr[:,x]
            e=cur&~left  # left edge
            e2=cur&~arr[:,x+1]
            for m in (e,e2):
                lab,n=ndi.label(m)
                if n:
                    s=ndi.sum(m,lab,range(1,n+1)).max()
                    best=max(best,int(s))
    return best
for ch,lst in used.items():
    for r,idx in lst:
        for i in idx:
            try:
                b=np.array(Image.open(f'nf/{ch}/{r}_{i:02d}_body.png'))[...,3]>0
                f=np.array(Image.open(f'nf/{ch}/{r}_{i:02d}_fx.png'))[...,3]>0
            except Exception as e: continue
            s=straight(b|f)
            if s>=40: print(ch,r,i,s)
