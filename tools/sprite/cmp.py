import json,sys
from PIL import Image,ImageDraw
import numpy as np
def comp(nfd,rows_frames,out):
    m=json.load(open(f'nf/{nfd}/meta.json'))
    tiles=[];
    for row,i in rows_frames:
        v=m[row]
        b=Image.open(f'nf/{nfd}/{row}_{i:02d}_body.png').convert('RGBA');f=Image.open(f'nf/{nfd}/{row}_{i:02d}_fx.png').convert('RGBA')
        f.alpha_composite(b);tiles.append((row,f,v['pivot']))
    L=max(p[0] for _,_,p in tiles);R=max(t.size[0]-p[0] for _,t,p in tiles);U=max(p[1] for _,_,p in tiles);D=8
    W=L+R+6;H=U+D+14
    sh=Image.new('RGBA',(W*len(tiles),H),(110,150,110,255));d=ImageDraw.Draw(sh)
    for k,(row,t,p) in enumerate(tiles):
        sh.alpha_composite(t,(k*W+L-p[0]+3,U-p[1]+14))
        d.text((k*W+2,0),row,fill=(255,255,255,255))
    for y in range(0,U,20): d.line([(0,U+14-y),(W*len(tiles),U+14-y)],fill=(0,0,0,90))
    sh.save(out);print(out,sh.size)
if __name__=='__main__':
    nfd=sys.argv[1];rf=[(a.split(':')[0],int(a.split(':')[1])) for a in sys.argv[3:]];comp(nfd,rf,sys.argv[2])
