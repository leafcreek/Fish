-- Fish's existing RGB protocol. No actions, SavedVariables or external libraries.
local VERSION, LOOT_HOLD, LOOT_GRACE = "0.1.0", 1.5, 1
local colors = {
    idle = { 1, 0, 0 }, fishing = { 0, 0, 1 }, loot = { 0, 1, 0 },
    full = { 0, 1, 1 }, dead = { 1, 165 / 255, 0 }, unknown = { 0, 0, 0 },
}
local fishingIDs = {
    [7620] = true, [7731] = true, [7732] = true, [18248] = true,
    [33095] = true, [51294] = true, [88868] = true, [110410] = true,
    [131474] = true, [131476] = true,
}
local fishingNames, restrictionStates = {}, {}
local initialized, enabled, eventDead, fullBag, eventCombat = false, true, false, false, false
local context, wasFishing, lootOpen, lootUntil = nil, false, false, 0
local wakeTimer, generation, testTimer, testToken = nil, 0, nil, nil
local fault, registrationFault, traceUntil = nil, nil, 0
local state, reason, channelID = "unknown", "Initialisierung", nil
local Refresh, Guard

local function Readable(value)
    if issecretvalue and issecretvalue(value) then return false end
    return not canaccessvalue or canaccessvalue(value)
end

local function Text(value)
    if not Readable(value) then return "<geschuetzt>" end
    return tostring(value)
end

local function Say(message)
    print("|cff66ccffFishHelper|r: " .. message)
end

local frame = CreateFrame("Frame", "FishHelperFrame", UIParent)
frame:SetFrameStrata("TOOLTIP")
frame:SetFrameLevel(10000)
frame:EnableMouse(false)
frame:SetAlpha(1)
if frame.SetIgnoreParentAlpha then frame:SetIgnoreParentAlpha(true) end
local pixel = frame:CreateTexture(nil, "OVERLAY")
pixel:SetAllPoints(frame)
pixel:SetBlendMode("DISABLE")
pixel:SetColorTexture(0, 0, 0, 1)

local function Paint(nextState, why)
    reason = why
    if state == nextState then return end
    state = nextState
    local rgb = colors[state]
    pixel:SetColorTexture(rgb[1], rgb[2], rgb[3], 1)
end

local function Layout()
    local scale = frame:GetEffectiveScale()
    if not Readable(scale) or type(scale) ~= "number" or scale <= 0 then
        error("FishHelper: UI-Skalierung nicht lesbar")
    end
    frame:ClearAllPoints()
    frame:SetPoint("TOPLEFT", UIParent, "TOPLEFT", 0, 0)
    frame:SetSize(4 / scale, 4 / scale)
end

local function CancelWake()
    generation = generation + 1
    if wakeTimer then wakeTimer:Cancel(); wakeTimer = nil end
end

local function ClearContext()
    CancelWake()
    context, wasFishing, lootOpen, lootUntil = nil, false, false, 0
end

local function StopTest()
    testToken = nil
    if testTimer then testTimer:Cancel(); testTimer = nil end
end

Guard = function(callback, ...)
    local ok, err = pcall(callback, ...)
    if not ok then
        fault = "Lua-Fehler; /fishhelper reset oder /reload"
        StopTest()
        ClearContext()
        Paint("unknown", fault)
        Say(fault .. ": " .. Text(err))
    end
end

local function WakeAt(deadline)
    local token = generation
    wakeTimer = C_Timer.NewTimer(math.max(0.01, deadline - GetTime()), function()
        if token ~= generation then return end
        wakeTimer = nil
        Guard(Refresh)
    end)
end

local function Restricted()
    if eventCombat then return true end
    if InCombatLockdown then
        local combat = InCombatLockdown()
        if not Readable(combat) or combat then return true end
    end
    local types = Enum and Enum.AddOnRestrictionType
    local states = Enum and Enum.AddOnRestrictionState
    if types and states and C_RestrictedActions and C_RestrictedActions.GetAddOnRestrictionState then
        for name, kind in pairs(types) do
            -- Chat restrictions alone do not restrict our status queries.
            if name ~= "Chat" then
                local active = restrictionStates[kind]
                if active == nil then
                    local value = C_RestrictedActions.GetAddOnRestrictionState(kind)
                    active = not Readable(value) or value ~= states.Inactive
                end
                if active then return true end
            end
        end
    end
    return false
end

local function LoadFishingNames()
    for id in pairs(fishingIDs) do
        local name
        if C_Spell and C_Spell.GetSpellName then name = C_Spell.GetSpellName(id) end
        if Readable(name) and type(name) == "string" then fishingNames[name] = true end
    end
end

local function ChannelKind()
    local name, _, _, _, _, _, _, id = UnitChannelInfo("player")
    if not Readable(name) or not Readable(id) then return "unknown" end
    channelID = id
    if name ~= nil or id ~= nil then
        if (id and fishingIDs[id]) or (name and fishingNames[name]) then return "fishing" end
        return "busy"
    end
    local cast = UnitCastingInfo("player")
    if not Readable(cast) then return "unknown" end
    return cast ~= nil and "busy" or "none"
end

Refresh = function()
    CancelWake()
    if testToken then return end
    if not initialized then Paint("unknown", "Welt wird geladen"); return end
    if not enabled then ClearContext(); Paint("unknown", "Deaktiviert"); return end
    if type(UnitIsDeadOrGhost) ~= "function" then Paint("unknown", "Lebensstatus-API fehlt"); return end
    local dead = UnitIsDeadOrGhost("player")
    if eventDead or (Readable(dead) and dead) then
        ClearContext(); Paint("dead", "Tot oder Geist"); return
    end
    if fault or not Readable(dead) or not frame:IsVisible() or Restricted() then
        ClearContext()
        Paint("unknown", fault or "UI oder API gesperrt")
        return
    end
    if fullBag then ClearContext(); Paint("full", "Inventarfehler bestaetigt"); return end

    local kind, now = ChannelKind(), GetTime()
    if kind == "unknown" or kind == "busy" then
        ClearContext()
        Paint("unknown", kind == "busy" and "Anderer Zauber aktiv" or "Kanalwerte geschuetzt")
        return
    end
    if kind == "fishing" then
        if not wasFishing then
            context = { signaled = false }
            lootOpen, lootUntil = false, 0
        end
        wasFishing = true
    elseif wasFishing then
        wasFishing = false
        if context then context.expires = now + LOOT_GRACE end
    end
    if context and context.expires and now >= context.expires and not lootOpen then context = nil end

    if lootOpen or now < lootUntil then
        Paint("loot", "Angel-Loot geoeffnet; keine Fisch-Stueckzahl")
        if now < lootUntil then WakeAt(lootUntil) end
    elseif kind == "fishing" then
        Paint("fishing", "Angelkanal aktiv")
    elseif context and context.expires then
        Paint("unknown", "Warte auf moegliches Angel-Loot")
        WakeAt(context.expires)
    else
        Paint("idle", "Bereit")
    end
end

local function Matches(value, expected)
    return expected ~= nil and Readable(value) and value == expected
end

local function InventoryError(errorType, message)
    local ids = Enum and Enum.UIERRORS
    return Matches(errorType, ids and ids.ERR_INV_FULL)
        or Matches(errorType, ids and ids.ERR_BAG_FULL)
        or Matches(errorType, LE_GAME_ERR_INV_FULL)
        or Matches(errorType, LE_GAME_ERR_BAG_FULL)
        or Matches(message, ERR_INV_FULL) or Matches(message, ERR_BAG_FULL)
end

local function ClearFullBagIfSpace()
    if not fullBag or not C_Container or not C_Container.GetContainerNumFreeSlots then return end
    -- Only normal carried bags. Neither bank nor specialist/reagent slots prove room for any catch.
    for bag = 0, math.min(NUM_BAG_SLOTS or 4, 4) do
        local free, family = C_Container.GetContainerNumFreeSlots(bag)
        if Readable(free) and Readable(family) and type(free) == "number" and free > 0
            and (family == 0 or (bag == 0 and family == nil)) then
            fullBag = false
            return
        end
    end
end

local function CheckAPIs()
    if registrationFault then fault = registrationFault end
    if type(UnitChannelInfo) ~= "function" or type(UnitCastingInfo) ~= "function"
        or not C_Timer or type(C_Timer.NewTimer) ~= "function" then
        fault = "Benoetigte Kanal-/Timer-API fehlt"
    end
end

local function HandleEvent(_, event, ...)
    if event == "UI_SCALE_CHANGED" or event == "DISPLAY_SIZE_CHANGED" then Layout(); return end
    StopTest()
    if event == "PLAYER_LEAVING_WORLD" then
        initialized = false
        ClearContext()
    elseif event == "PLAYER_ENTERING_WORLD" then
        initialized, eventDead, eventCombat = true, false, false
        restrictionStates = {}
        ClearContext()
        CheckAPIs()
        LoadFishingNames()
        ClearFullBagIfSpace()
        Layout()
    elseif event == "PLAYER_DEAD" then
        eventDead = true
    elseif event == "PLAYER_ALIVE" or event == "PLAYER_UNGHOST" then
        eventDead = false
    elseif event == "PLAYER_REGEN_DISABLED" then
        eventCombat = true
    elseif event == "PLAYER_REGEN_ENABLED" then
        eventCombat = false
    elseif event == "ADDON_RESTRICTION_STATE_CHANGED" then
        local kind, value = ...
        local states = Enum and Enum.AddOnRestrictionState
        if Readable(kind) and kind ~= nil and Readable(value) and states then
            -- The API may still report Inactive during the Activating event: trust its payload.
            restrictionStates[kind] = value ~= states.Inactive
        end
    elseif event == "UI_ERROR_MESSAGE" then
        if InventoryError(...) then fullBag = true end
    elseif event == "BAG_UPDATE_DELAYED" then
        ClearFullBagIfSpace()
    elseif event == "LOOT_OPENED" then
        local now = GetTime()
        if context and (wasFishing or not context.expires or now < context.expires)
            and not context.signaled then
            context.signaled = true
            lootOpen, lootUntil = true, now + LOOT_HOLD
        end
    elseif event == "LOOT_CLOSED" then
        lootOpen = false
    end
    Refresh()
    if GetTime() < traceUntil then
        Say(event .. " -> " .. state .. " (" .. reason .. "), spell=" .. Text(channelID))
    end
end

local function Status()
    local version, build, _, interface = GetBuildInfo()
    Say("v" .. VERSION .. " | " .. state .. " | " .. reason)
    Say("WoW " .. Text(version) .. ", Build " .. Text(build) .. ", Interface " .. Text(interface)
        .. "; Kanal-ID " .. Text(channelID))
end

local function ColorTest()
    StopTest()
    ClearContext()
    local token, index = {}, 0
    testToken = token
    local sequence = { "unknown", "idle", "fishing", "loot", "full", "dead" }
    Say("Farbtest fuer 12 s: Fish muss gestoppt sein. Spielereignisse brechen den Test ab.")
    local function Step()
        if testToken ~= token then return end
        index = index + 1
        if index > #sequence then StopTest(); Refresh(); Say("Farbtest beendet."); return end
        Paint(sequence[index], "Farbtest")
        testTimer = C_Timer.NewTimer(2, function() Guard(Step) end)
    end
    Step()
end

local function Command(message)
    local command = message:lower():match("^%s*(%S*)")
    if command == "test" then ColorTest()
    elseif command == "off" then
        StopTest(); enabled = false; ClearContext(); Refresh(); Say("Deaktiviert (schwarz).")
    elseif command == "on" or command == "reset" then
        StopTest()
        enabled = true
        if command == "reset" then fullBag, fault = false, nil end
        ClearContext(); CheckAPIs(); LoadFishingNames(); Layout(); Refresh(); Status()
    elseif command == "trace" then
        traceUntil = GetTime() + 60
        Say("Ereignisdiagnose fuer 60 s aktiviert; geschuetzte Payloads werden nicht ausgegeben.")
    elseif command == "" or command == "status" then Status()
    else Say("/fishhelper status | test | trace | off | on | reset") end
end

frame:SetScript("OnEvent", function(...) Guard(HandleEvent, ...) end)
frame:SetScript("OnShow", function() Guard(function() Layout(); Refresh() end) end)
frame:SetScript("OnHide", function()
    StopTest(); ClearContext(); Paint("unknown", "UI ausgeblendet")
end)
local events = {
    "PLAYER_ENTERING_WORLD", "PLAYER_LEAVING_WORLD", "PLAYER_DEAD", "PLAYER_ALIVE", "PLAYER_UNGHOST",
    "PLAYER_REGEN_DISABLED", "PLAYER_REGEN_ENABLED", "ADDON_RESTRICTION_STATE_CHANGED",
    "LOOT_OPENED", "LOOT_CLOSED", "UI_ERROR_MESSAGE", "BAG_UPDATE_DELAYED",
    "UI_SCALE_CHANGED", "DISPLAY_SIZE_CHANGED",
}
for _, event in ipairs(events) do
    local ok, registered = pcall(frame.RegisterEvent, frame, event)
    if not ok or registered == false then registrationFault = "Event nicht verfuegbar: " .. event end
end
local unitEvents = {
    "UNIT_SPELLCAST_START", "UNIT_SPELLCAST_STOP", "UNIT_SPELLCAST_FAILED", "UNIT_SPELLCAST_INTERRUPTED",
    "UNIT_SPELLCAST_CHANNEL_START", "UNIT_SPELLCAST_CHANNEL_STOP", "UNIT_SPELLCAST_CHANNEL_UPDATE",
}
for _, event in ipairs(unitEvents) do
    -- Filter in the client. Never compare a potentially secret event unit or spellID in Lua.
    local ok, registered = pcall(frame.RegisterUnitEvent, frame, event, "player")
    if not ok or registered == false then registrationFault = "Spieler-Event nicht verfuegbar: " .. event end
end
SLASH_FISHHELPER1 = "/fishhelper"
SlashCmdList.FISHHELPER = function(message) Guard(Command, message) end
Guard(function() CheckAPIs(); Layout(); frame:Show() end)
