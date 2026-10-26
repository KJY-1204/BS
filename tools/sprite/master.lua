local spr = Sprite(166,159,ColorMode.RGB)
spr.layers[1].name = "streamers"
spr:newCel(spr.layers[1], 1, Image{fromFile=app.params["dir"].."/m_streamers.png"}, Point(0,0))
local l2 = spr:newLayer(); l2.name = "pole"
spr:newCel(l2, 1, Image{fromFile=app.params["dir"].."/m_pole.png"}, Point(0,0))
local l3 = spr:newLayer(); l3.name = "body"
spr:newCel(l3, 1, Image{fromFile=app.params["dir"].."/m_body.png"}, Point(0,0))
spr:saveAs(app.params["out"])
