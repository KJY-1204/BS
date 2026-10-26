local m = dofile(app.params["manifest"])
local spr = Sprite(m.w, m.h, ColorMode.RGB)
local names = {"fx_back","body","fx_front"}
local layers = {}
spr.layers[1].name = names[1]; layers[names[1]] = spr.layers[1]
for i=2,#names do local l = spr:newLayer(); l.name = names[i]; layers[names[i]] = l end
for fi,f in ipairs(m.frames) do
  local frame = (fi==1) and spr.frames[1] or spr:newFrame()
  frame.duration = f.dur/1000
  for _,ln in ipairs(names) do
    local img = Image{ fromFile = f.layers[ln] }
    spr:newCel(layers[ln], frame, img, Point(0,0))
  end
end
for _,t in ipairs(m.tags) do
  local tag = spr:newTag(t.start, t.end_)
  tag.name = t.name
end
spr:saveAs(app.params["out"])
