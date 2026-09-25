-- Original pixel placement in Aseprite. One-time intake; saved .aseprite owns all later edits.
-- No procedural noise, mirrored variants, geometry, fill level, or directional flow animation.
local destination = app.params.output
assert(destination and destination ~= "", "Supply --script-param output=<new master.aseprite>")
local existing = io.open(destination, "rb")
if existing then existing:close(); error("Refusing to replace an existing native master") end

local sprite = Sprite(32, 32, ColorMode.RGB)
local colors = {
  Color{r=43,g=89,b=103,a=255},   -- shade
  Color{r=50,g=102,b=115,a=255},  -- quiet surface
  Color{r=59,g=116,b=125,a=255},  -- broad reflected tone
  Color{r=73,g=133,b=138,a=255},  -- ripple shoulder
  Color{r=101,g=155,b=157,a=255}, -- restrained crest
  Color{r=140,g=181,b=173,a=255}  -- occasional soft light, never white sparkle
}
local palette = Palette(#colors)
for index, color in ipairs(colors) do palette:setColor(index - 1, color) end
sprite:setPalette(palette)
local body = sprite.layers[1]
body.name = "Surface body"
local tones = sprite:newLayer()
tones.name = "Quiet reflected tones"
local ripples = sprite:newLayer()
ripples.name = "Cosmetic ripple accents"
for frame = 2, 8 do sprite:newEmptyFrame(frame) end

-- Hand-placed inclusive horizontal pixel runs: y, left, right, palette entry.
-- Both variants share a quiet boundary colour so any neighbours meet without a colour seam.
local toneRuns = {
  {
    {4,5,11,1},{5,3,14,1},{6,4,16,1},{7,7,13,1},
    {10,21,25,3},{11,18,28,3},{12,19,27,3},{13,22,26,3},
    {17,4,11,3},{18,2,14,3},{19,4,12,3},{20,7,10,3},
    {23,19,24,1},{24,16,28,1},{25,18,29,1},{26,21,26,1},
    {28,6,10,3},{29,4,12,3}
  },
  {
    {3,18,24,3},{4,15,27,3},{5,17,28,3},{6,20,25,3},
    {9,4,9,1},{10,2,13,1},{11,3,15,1},{12,6,12,1},
    {15,19,25,1},{16,16,29,1},{17,18,27,1},{18,21,24,1},
    {22,7,14,3},{23,4,17,3},{24,5,16,3},{25,8,13,3},
    {28,22,26,3},{29,19,28,3}
  }
}
local rippleRuns = {
  {
    {{8,5,10,4},{9,4,5,3},{9,11,13,3},{18,20,23,5},{19,18,19,4},{19,24,25,3},{27,8,11,4}},
    {{8,6,9,4},{9,4,5,4},{9,10,14,4},{10,6,8,3},{18,20,22,4},{19,18,19,3},{27,7,12,4},{28,6,7,3}},
    {{9,5,10,4},{10,4,5,3},{10,11,13,3},{18,19,23,4},{19,17,18,3},{19,24,25,4},{27,8,10,5},{28,6,7,4}},
    {{8,6,10,4},{9,4,5,3},{9,11,12,4},{18,20,24,5},{19,18,19,4},{27,8,12,4},{28,7,8,3}}
  },
  {
    {{7,19,23,4},{8,17,18,3},{8,24,26,4},{15,5,9,4},{16,4,5,3},{24,21,24,5},{25,19,20,4}},
    {{7,20,24,5},{8,18,19,4},{8,25,26,3},{15,6,10,4},{16,4,5,4},{16,11,12,3},{24,21,23,4},{25,19,20,3}},
    {{7,20,23,4},{8,18,19,3},{15,5,10,5},{16,3,4,3},{16,11,12,4},{24,20,24,4},{25,18,19,3},{25,25,26,4}},
    {{7,19,24,4},{8,17,18,4},{8,25,26,3},{15,5,9,4},{16,4,5,3},{24,21,25,5},{25,19,20,4},{25,26,26,3}}
  }
}
local function paint(image, runs)
  for _, run in ipairs(runs) do
    for x = run[2], run[3] do image:drawPixel(x, run[1], colors[run[4]]) end
  end
end
for variant = 1, 2 do
  for phase = 1, 4 do
    local frame = (variant - 1) * 4 + phase
    sprite.frames[frame].duration = 0.240
    local base = Image(32,32,ColorMode.RGB)
    base:clear(colors[2])
    sprite:newCel(body, frame, base, Point(0,0))
    local tone = Image(32,32,ColorMode.RGB)
    paint(tone, toneRuns[variant])
    sprite:newCel(tones, frame, tone, Point(0,0))
    local ripple = Image(32,32,ColorMode.RGB)
    paint(ripple, rippleRuns[variant][phase])
    sprite:newCel(ripples, frame, ripple, Point(0,0))
  end
end
local first = sprite:newTag(1,4)
first.name = "surface_a"
first.aniDir = AniDir.FORWARD
local second = sprite:newTag(5,8)
second.name = "surface_b"
second.aniDir = AniDir.FORWARD
assert(sprite:saveAs(destination), "Native master save failed")
print("WATER_SURFACE_MASTER_CREATED 32x32 8frames 240ms 3layers 2tags")
