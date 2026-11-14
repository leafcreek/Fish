-- Run from the repository root: lua Tests/FishHelper.Tests.lua
-- Lua 5.1+ or Fengari. These mocks cannot reproduce WoW's real secret/taint VM.
local addonPath = arg[1] or "addon/FishHelper/FishHelper.lua"
local tests, assertions = 0, 0
local secret = setmetatable({}, { __tostring = function() error("secret was converted to text") end })
local function Check(condition, message)
    assertions = assertions + 1
    assert(condition, message)
end

local function Client(options)
    options = options or {}
    local c = { now = 0, timers = {}, messages = {}, restrictions = {}, bags = {}, bagReads = 0, scale = 1 }
    local env = setmetatable({}, { __index = _G })
    local function Accessible(value) return not rawequal(value, secret) end
    env.issecretvalue = function(value) return not Accessible(value) end
    env.canaccessvalue = Accessible
    env.UIParent = {}
    env.SlashCmdList = {}
    env.print = function(message) c.messages[#c.messages + 1] = message end
    env.GetTime = function() return c.now end
    env.GetBuildInfo = function() return "1.60.1", "70790", "Nov 3 2026", 16001 end
    env.UnitIsDeadOrGhost = function() return c.dead or false end
    env.InCombatLockdown = function() return c.combat or false end
    env.UnitCastingInfo = function() return c.cast end
    env.UnitChannelInfo = function(unit)
        assert(unit == "player")
        if c.queryError then error("simulated query error") end
        return c.channelName, nil, nil, nil, nil, nil, nil, c.channelID
    end
    env.C_Spell = { GetSpellName = function() return "Angeln" end }
    env.C_Container = { GetContainerNumFreeSlots = function(bag)
        c.bagReads = c.bagReads + 1
        local slots = c.bags[bag] or { 0, 0 }
        return slots[1], slots[2]
    end }
    env.NUM_BAG_SLOTS = 4
    env.ERR_INV_FULL, env.ERR_BAG_FULL = "Inventar ist voll.", "Tasche ist voll."
    env.LE_GAME_ERR_INV_FULL, env.LE_GAME_ERR_BAG_FULL = 1001, 1002
    env.Enum = {
        UIERRORS = { ERR_INV_FULL = 2001, ERR_BAG_FULL = 2002 },
        AddOnRestrictionType = { Combat = 0, Encounter = 1, ChallengeMode = 2, PvPMatch = 3, Map = 4, Chat = 5 },
        AddOnRestrictionState = { Inactive = 0, Activating = 1, Active = 2 },
    }
    env.C_RestrictedActions = { GetAddOnRestrictionState = function(kind)
        return c.restrictions[kind] or 0
    end }
    env.C_Timer = { NewTimer = function(delay, callback)
        local timer = { due = c.now + delay, callback = callback }
        function timer:Cancel() self.cancelled = true end
        c.timers[#c.timers + 1] = timer
        return timer
    end }
    env.CreateFrame = function(_, name, parent)
        local f = { scripts = {}, events = {}, units = {}, shown = true }
        c.frame = f
        Check(name == "FishHelperFrame" and parent == env.UIParent, "named UIParent frame")
        function f:SetFrameStrata(value) self.strata = value end
        function f:SetFrameLevel(value) self.level = value end
        function f:EnableMouse(value) self.mouse = value end
        function f:SetAlpha(value) self.alpha = value end
        function f:SetIgnoreParentAlpha(value) self.ignoreAlpha = value end
        function f:ClearAllPoints() end
        function f:SetPoint(...) self.anchor = { ... } end
        function f:SetSize(w, h) self.width, self.height = w, h end
        function f:GetEffectiveScale() return c.scale end
        function f:IsVisible() return self.shown end
        function f:Show()
            local changed = not self.shown
            self.shown = true
            if changed and self.scripts.OnShow then self.scripts.OnShow(self) end
        end
        function f:Hide()
            self.shown = false
            if self.scripts.OnHide then self.scripts.OnHide(self) end
        end
        function f:CreateTexture()
            local texture = {}
            function texture:SetAllPoints(target) Check(target == f, "texture fills frame") end
            function texture:SetBlendMode(mode) self.blendMode = mode end
            function texture:SetColorTexture(r, g, b, a)
                c.rgb = math.floor(r * 255 + 0.5) * 65536 + math.floor(g * 255 + 0.5) * 256
                    + math.floor(b * 255 + 0.5)
                Check(a == 1, "opaque signal")
            end
            c.texture = texture
            return texture
        end
        function f:SetScript(event, callback) self.scripts[event] = callback end
        function f:RegisterEvent(event)
            if event == options.unsupportedEvent then return false end
            self.events[event] = true
            return true
        end
        function f:RegisterUnitEvent(event, unit)
            Check(unit == "player", "filter unit events in client")
            self.units[event] = unit
            return self:RegisterEvent(event)
        end
        return f
    end
    local chunk
    if setfenv then chunk = setfenv(assert(loadfile(addonPath)), env)
    else chunk = assert(loadfile(addonPath, "t", env)) end
    chunk("FishHelper", {})
    function c:Emit(event, ...)
        if not self.frame.events[event] then return end
        local unit = ...
        if self.frame.units[event] and type(unit) == "string" and unit ~= "player" then return end
        self.frame.scripts.OnEvent(self.frame, event, ...)
    end
    function c:Advance(seconds)
        local target, count = self.now + seconds, 0
        while true do
            local nextTimer
            for _, timer in ipairs(self.timers) do
                if not timer.cancelled and not timer.ran and timer.due <= target
                    and (not nextTimer or timer.due < nextTimer.due) then nextTimer = timer end
            end
            if not nextTimer then break end
            count = count + 1
            assert(count < 1000, "unbounded timer loop")
            self.now, nextTimer.ran = nextTimer.due, true
            nextTimer.callback()
        end
        self.now = target
    end
    function c:Command(message) env.SlashCmdList.FISHHELPER(message) end
    function c:Channel(id, name)
        self.channelID = id
        self.channelName = name or (id and "Angeln" or nil)
    end
    function c:Start(id)
        self:Channel(id or 7620)
        self:Emit("UNIT_SPELLCAST_CHANNEL_START", "player", "cast", id or 7620)
    end
    function c:Stop()
        self:Channel(nil)
        self:Emit("UNIT_SPELLCAST_CHANNEL_STOP", "player", "cast", 7620)
    end
    function c:Color(rgb, message) Check(self.rgb == rgb, message or string.format("expected #%06X, got #%06X", rgb, self.rgb)) end
    function c:Enter() self:Emit("PLAYER_ENTERING_WORLD") end
    c.env = env
    return c
end

local function Test(name, callback)
    callback()
    tests = tests + 1
    print("OK " .. name)
end

Test("startup and initial synchronization", function()
    local c = Client()
    c:Color(0)
    c:Enter(); c:Color(0xff0000)
    Check(c.frame.scripts.OnUpdate == nil and #c.timers == 0, "no idle polling")
    Check(c.bagReads == 0, "no routine bag scan")
    Check(c.frame.mouse == false and c.texture.blendMode == "DISABLE", "noninteractive solid signal")
end)

Test("reload attaches to an ongoing fishing channel", function()
    local c = Client()
    c:Channel(7731); c:Enter(); c:Color(0x0000ff)
end)

Test("all configured fishing ranks", function()
    for _, id in ipairs({ 7620, 7731, 7732, 18248, 33095, 51294, 88868, 110410, 131474, 131476 }) do
        local c = Client(); c:Enter(); c:Start(id); c:Color(0x0000ff)
    end
end)

Test("localized name fallback and unrelated channels", function()
    local c = Client(); c:Enter()
    c:Start(99999); c:Color(0x0000ff)
    c:Channel(99998, "Anderer Kanal"); c:Emit("UNIT_SPELLCAST_CHANNEL_START", "player"); c:Color(0)
    c:Channel(nil); c.cast = "Feuerball"; c:Emit("UNIT_SPELLCAST_START", "player"); c:Color(0)
    c.cast = nil; c:Emit("UNIT_SPELLCAST_STOP", "player"); c:Color(0xff0000)
end)

Test("secret values are not compared, indexed or printed", function()
    local c = Client(); c:Enter()
    c:Channel(secret, secret); c:Emit("UNIT_SPELLCAST_CHANNEL_START", "player"); c:Color(0)
    c:Channel(secret, "Angeln"); c:Emit("UNIT_SPELLCAST_CHANNEL_UPDATE", "player"); c:Color(0)
    c:Channel(7620); c:Emit("UNIT_SPELLCAST_CHANNEL_START", secret, secret, secret); c:Color(0x0000ff)
    c:Channel(nil); c.cast = secret; c:Emit("UNIT_SPELLCAST_START", "player"); c:Color(0)
    Check(#c.messages == 0, "no secret conversion or caught runtime error")
end)

Test("non-player casts never affect the signal", function()
    local c = Client(); c:Enter()
    c:Channel(7620); c:Emit("UNIT_SPELLCAST_CHANNEL_START", "target"); c:Color(0xff0000)
end)

Test("channel stop has a bounded loot grace period", function()
    local c = Client(); c:Enter(); c:Start(); c:Stop(); c:Color(0)
    c:Advance(0.99); c:Color(0)
    c:Advance(0.02); c:Color(0xff0000)
end)

Test("fast auto-loot is latched and duplicates do not extend it", function()
    local c = Client(); c:Enter(); c:Start(); c:Stop(); c:Advance(0.2)
    c:Emit("LOOT_OPENED", true, false); c:Color(0x00ff00)
    c:Emit("LOOT_CLOSED"); c:Color(0x00ff00)
    c:Advance(0.5); c:Emit("LOOT_OPENED", true, false); c:Emit("LOOT_CLOSED")
    c:Advance(0.99); c:Color(0x00ff00)
    c:Advance(0.02); c:Color(0xff0000)
end)

Test("loot can precede channel stop and stays green while open", function()
    local c = Client(); c:Enter(); c:Start(); c:Emit("LOOT_OPENED"); c:Color(0x00ff00)
    c:Stop(); c:Advance(3); c:Color(0x00ff00)
    c:Emit("LOOT_CLOSED"); c:Color(0xff0000)
end)

Test("unrelated loot and expired fishing context do not confirm catches", function()
    local c = Client(); c:Enter(); c:Emit("LOOT_OPENED"); c:Color(0xff0000)
    c:Emit("LOOT_CLOSED"); c:Start(); c:Stop(); c:Advance(1.1)
    c:Emit("LOOT_OPENED"); c:Color(0xff0000)
end)

Test("new cast invalidates an old loot timer", function()
    local c = Client(); c:Enter(); c:Start(); c:Emit("LOOT_OPENED"); c:Stop(); c:Emit("LOOT_CLOSED")
    local stale = c.timers[#c.timers]
    c:Advance(0.2); c:Start(7731); c:Color(0x0000ff)
    stale.callback(); c:Color(0x0000ff)
    c:Advance(2); c:Color(0x0000ff)
end)

Test("zero free slots alone does not signal full", function()
    local c = Client(); c:Enter(); c:Emit("BAG_UPDATE_DELAYED"); c:Color(0xff0000)
    Check(c.bagReads == 0, "stack capacity is not guessed from free slots")
end)

Test("full inventory recognizes localized and numeric errors", function()
    for _, args in ipairs({ { 0, "Inventar ist voll." }, { 2001, "" }, { 1002, "" }, { 0, "Tasche ist voll." } }) do
        local c = Client(); c:Enter(); c:Emit("UI_ERROR_MESSAGE", args[1], args[2]); c:Color(0x00ffff)
    end
end)

Test("secret or unrelated errors never become full inventory", function()
    local c = Client(); c:Enter()
    c:Emit("UI_ERROR_MESSAGE", secret, secret); c:Color(0xff0000)
    c:Emit("UI_ERROR_MESSAGE", 17, "Kein Ziel"); c:Color(0xff0000)
    Check(#c.messages == 0, "no secret message formatting")
end)

Test("only general carried bag capacity clears a full error", function()
    local c = Client(); c:Enter(); c:Emit("UI_ERROR_MESSAGE", 2001, "")
    c.env.NUM_BAG_SLOTS = 5
    c.bags[1] = { 10, 32 }; c.bags[5] = { 10, 0 }
    c:Emit("BAG_UPDATE_DELAYED"); c:Color(0x00ffff)
    c.bags[2] = { secret, secret }; c:Emit("BAG_UPDATE_DELAYED"); c:Color(0x00ffff)
    c.bags[0] = { 1, 0 }; c:Emit("BAG_UPDATE_DELAYED"); c:Color(0xff0000)
end)

Test("death event takes priority before the unit query updates", function()
    local c = Client(); c:Enter(); c:Start(); c:Emit("LOOT_OPENED")
    c:Emit("UI_ERROR_MESSAGE", 2001, ""); c:Emit("PLAYER_DEAD"); c:Color(0xffa500)
    c:Advance(5); c:Color(0xffa500)
    c:Channel(nil); c:Emit("PLAYER_ALIVE"); c:Color(0x00ffff)
end)

Test("ghost and resurrection synchronize on login", function()
    local c = Client(); c.dead = true; c:Enter(); c:Color(0xffa500)
    c.dead = false; c:Emit("PLAYER_UNGHOST"); c:Color(0xff0000)
end)

Test("combat cancels loot and never replays it later", function()
    local c = Client(); c:Enter(); c:Start(); c:Emit("LOOT_OPENED")
    c.combat = true; c:Emit("PLAYER_REGEN_DISABLED"); c:Color(0)
    c:Channel(nil); c:Advance(3); c:Color(0)
    c.combat = false; c:Emit("PLAYER_REGEN_ENABLED"); c:Color(0xff0000)
end)

Test("combat event blocks even before the combat API updates", function()
    local c = Client(); c:Enter(); c:Start()
    c:Emit("PLAYER_REGEN_DISABLED"); c:Color(0)
    c:Emit("PLAYER_REGEN_ENABLED"); c:Color(0x0000ff)
end)

Test("unreadable death and combat queries stay black", function()
    local c = Client(); c.dead = secret; c:Enter(); c:Color(0)
    c.dead = false; c.combat = secret; c:Emit("BAG_UPDATE_DELAYED"); c:Color(0)
    c.combat = false; c:Emit("BAG_UPDATE_DELAYED"); c:Color(0xff0000)
end)

Test("restriction Activating payload wins over an inactive API query", function()
    local c = Client(); c:Enter(); c:Start()
    c:Emit("ADDON_RESTRICTION_STATE_CHANGED", 1, 1); c:Color(0)
    c.restrictions[1] = 2
    c:Emit("ADDON_RESTRICTION_STATE_CHANGED", 1, 0); c:Color(0x0000ff)
end)

Test("restrictions present at world entry are honored", function()
    local c = Client(); c.restrictions[4] = 2; c:Enter(); c:Color(0)
end)

Test("chat-only restrictions do not block fishing", function()
    local c = Client(); c.restrictions[5] = 2; c:Enter(); c:Start(); c:Color(0x0000ff)
end)

Test("leaving the world invalidates queued callbacks", function()
    local c = Client(); c:Enter(); c:Start(); c:Stop()
    local stale = c.timers[#c.timers]
    c:Emit("PLAYER_LEAVING_WORLD"); c:Color(0)
    stale.callback(); c:Advance(4); c:Color(0)
    c:Channel(7731); c:Enter(); c:Color(0x0000ff)
end)

Test("hidden UI suspends state and showing it resynchronizes", function()
    local c = Client(); c:Enter(); c:Start(); c.frame:Hide(); c:Color(0)
    c:Emit("BAG_UPDATE_DELAYED"); c:Color(0)
    c.frame:Show(); c:Color(0x0000ff)
end)

Test("pixel size follows effective UI scale", function()
    local c = Client(); c:Enter()
    c.scale = 0.64; c:Emit("UI_SCALE_CHANGED")
    Check(math.abs(c.frame.width * c.scale - 4) < 0.00001, "four physical pixels")
    Check(c.frame.anchor[1] == "TOPLEFT" and c.frame.anchor[4] == 0 and c.frame.anchor[5] == 0, "top left origin")
    c.scale = 1; c:Emit("DISPLAY_SIZE_CHANGED"); Check(c.frame.width == 4, "resolution change")
end)

Test("runtime errors latch black until explicit reset", function()
    local c = Client(); c:Enter(); c.queryError = true; c:Emit("UNIT_SPELLCAST_START", "player"); c:Color(0)
    c.queryError = false; c:Emit("BAG_UPDATE_DELAYED"); c:Color(0)
    c:Command("reset"); c:Color(0xff0000)
end)

Test("missing required events cannot be bypassed by reset", function()
    local c = Client({ unsupportedEvent = "UNIT_SPELLCAST_CHANNEL_STOP" })
    c:Enter(); c:Color(0)
    c:Command("reset"); c:Color(0)
end)

Test("missing required APIs stay black", function()
    local c = Client(); c.env.UnitChannelInfo = false; c:Enter(); c:Color(0)
    c:Command("reset"); c:Color(0)
end)

Test("off survives events and on synchronizes current state", function()
    local c = Client(); c:Enter(); c:Command("off"); c:Start(); c:Color(0)
    c:Command("on"); c:Color(0x0000ff)
end)

Test("bounded color test returns to live state", function()
    local c = Client(); c:Enter(); c:Command("test"); c:Color(0)
    for _, rgb in ipairs({ 0xff0000, 0x0000ff, 0x00ff00, 0x00ffff, 0xffa500 }) do
        c:Advance(2); c:Color(rgb)
    end
    c:Advance(2); c:Color(0xff0000)
    Check(c.frame.scripts.OnUpdate == nil, "color test does not add polling")
end)

Test("game events and off invalidate color test callbacks", function()
    local c = Client(); c:Enter(); c:Command("test")
    local stale = c.timers[#c.timers]
    c:Emit("PLAYER_DEAD"); c:Color(0xffa500)
    stale.callback(); c:Color(0xffa500)
    c:Command("test"); stale = c.timers[#c.timers]
    c:Command("off"); stale.callback(); c:Color(0)
end)

Test("trace is bounded and status reports client metadata", function()
    local c = Client(); c:Enter(); c:Command("trace"); c:Start()
    local before = #c.messages
    c:Advance(61); c:Emit("UNIT_SPELLCAST_CHANNEL_UPDATE", secret, secret, secret)
    Check(#c.messages == before, "trace expires after sixty seconds")
    c:Command("status")
    Check(c.messages[#c.messages]:find("70790", 1, true) ~= nil, "build included")
end)

print(string.format("FishHelper: %d tests, %d assertions passed.", tests, assertions))
