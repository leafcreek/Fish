-- Executed by Build.mjs with the libraries and FishAuraSource preloaded.
local serialize, deflate = LibStub("LibSerialize"), LibStub("LibDeflate")
local onShow = assert(FishAuraSource)
local onHide = "if aura_env.fishStop then aura_env.fishStop() end"
local aura = {
    id = "Fish - Statuspixel", uid = "FishPixel01", regionType = "texture",
    internalVersion = 91, version = 1, semver = "1.0.0",
    authorOptions = {}, config = {}, conditions = {}, subRegions = {},
    information = {},
    -- The visible signal is a child frame whose lifecycle follows this region.
    texture = "Interface\\Buttons\\WHITE8X8", color = { 0, 0, 0, 0 }, alpha = 1,
    width = 4, height = 4, blendMode = "BLEND", desaturate = false,
    textureWrapMode = "CLAMPTOBLACKADDITIVE", rotation = 0, rotate = false, mirror = false,
    selfPoint = "TOPLEFT", anchorPoint = "TOPLEFT", anchorFrameType = "SCREEN",
    xOffset = 0, yOffset = 0, frameStrata = 5,
    load = { class = { multi = {} }, spec = { multi = {} }, size = { multi = {} } },
    triggers = {
        { trigger = { type = "custom", custom_type = "status", check = "event",
            events = "PLAYER_ENTERING_WORLD", custom = "function() return true end" },
          untrigger = { custom = "function() return false end" } },
        activeTriggerMode = -10,
    },
    actions = {
        init = { do_custom = false },
        start = { do_custom = true, custom = onShow },
        finish = { do_custom = true, custom = onHide },
    },
    animation = {
        start = { type = "none", duration_type = "seconds" },
        main = { type = "none", duration_type = "seconds" },
        finish = { type = "none", duration_type = "seconds" },
    },
    desc = "Fish: Rot=bereit, Blau=Angeln, Gruen=Loot, Tuerkis=Inventar voll, Orange=tot, Schwarz=unbekannt/gesperrt. FishHelper deaktivieren. Noch nicht im Spiel getestet.",
}
local transmit = { m = "d", d = aura, v = 1421, s = "1.4.0" }
local serialized = serialize:SerializeEx({ errorOnUnserializableType = false }, transmit)
FishAuraSerialized = serialized
-- Raw DEFLATE is supplied by Node's zlib (Fengari uses 32-bit integers).
function FishAuraEncode(compressed)
    -- This is the prefix emitted by ForeverAuras 1.4.0 Transmission.lua itself.
    local import = "!WA:2!" .. deflate:EncodeForPrint(compressed)
    local decoded = assert(deflate:DecodeForPrint(import:sub(7)))
    local success, restored = serialize:Deserialize(assert(deflate:DecompressDeflate(decoded)))
    assert(success and restored.m == "d" and restored.v == 1421)
    assert(restored.d.actions.start.custom == onShow and restored.d.actions.finish.custom == onHide)
    assert(restored.d.id == aura.id and restored.d.triggers[1].trigger.custom_type == "status")
    FishAuraImport = import
    FishAuraRestored = restored.d
    print("ForeverAuras import generated and decoded successfully: " .. #import .. " characters.")
end
