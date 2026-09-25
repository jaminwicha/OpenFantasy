-- Read-only native-editor preview: a 2x2 arrangement with staggered cosmetic phases.
local source = app.open(app.params.source)
assert(source and #source.frames == 8, "Expected eight-frame water master")
local preview = Sprite(64,64,ColorMode.RGB)
preview.layers[1].name = "Preview only - flattened from master"
for frame = 2,4 do preview:newEmptyFrame(frame) end
for frame = 1,4 do
  preview.frames[frame].duration = 0.240
  local image = Image(64,64,ColorMode.RGB)
  image:drawSprite(source, frame, Point(0,0))
  image:drawSprite(source, frame + 4, Point(32,0))
  image:drawSprite(source, (frame + 1) % 4 + 5, Point(0,32))
  image:drawSprite(source, frame % 4 + 1, Point(32,32))
  preview:newCel(preview.layers[1], frame, image, Point(0,0))
end
assert(preview:saveAs(app.params.animation), "Animation preview export failed")
local first = Image(64,64,ColorMode.RGB)
first:drawSprite(preview, 1)
assert(first:saveAs(app.params.still), "Still preview export failed")
print("NATIVE_PREVIEW_EXPORTED 64x64 4frames 240ms; master unchanged")
