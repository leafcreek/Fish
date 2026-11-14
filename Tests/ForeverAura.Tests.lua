-- Reuse the existing WoW simulation and state cases against the decoded export.
local source = assert(FishHelperTestSource)
local harness = source:sub(1, assert(source:find('Test("startup', 1, true)) - 1)
harness = harness:gsub('Check%(name == "FishHelperFrame" and parent == env.UIParent, "named UIParent frame"%)',
    'Check(name == nil and parent == env.aura_env.region, "anonymous aura child frame")')
harness = harness:gsub('function f:SetScript%(event, callback%)',
    'function f:UnregisterAllEvents() self.events, self.units = {}, {} end\n        function f:SetScript(event, callback)')
local first = assert(harness:find('    local chunk\n', 1, true))
local last = assert(harness:find('    function c:Emit', first, true))
harness = harness:sub(1, first - 1) .. [[
    env.aura_env = { region = {} }
    -- Model forbidden APIs: accessing them fails this test immediately.
    for _, key in ipairs({ "pcall", "xpcall", "SlashCmdList", "loadstring", "getfenv", "setfenv" }) do
        env[key] = false
    end
    local chunk
    if setfenv then chunk = setfenv(assert(loadstring(FishAuraRestored.actions.start.custom)), env)
    else chunk = assert(load(FishAuraRestored.actions.start.custom, "On Show", "t", env)) end
    c.activate = chunk
    if setfenv then c.deactivate = setfenv(assert(loadstring(FishAuraRestored.actions.finish.custom)), env)
    else c.deactivate = assert(load(FishAuraRestored.actions.finish.custom, "On Hide", "t", env)) end
    chunk()
]] .. harness:sub(last)
harness = harness .. '\nreturn Client, Test, Check, secret, function() return tests, assertions end\n'
local Client, Test, Check, secret, Counts = assert((loadstring or load)(harness, "shared WoW test harness"))()
local firstCase = assert(source:find('Test("startup', 1, true))
local lastCase = assert(source:find('Test("runtime errors', firstCase, true))
local cases = source:sub(firstCase, lastCase - 1)
cases = cases:gsub('c:Color%(0%)\n    c:Enter', 'c:Color(0xff0000)\n    c:Enter', 1)
local env = setmetatable({ Client = Client, Test = Test, Check = Check, secret = secret }, { __index = _G })
local run
if setfenv then run = setfenv(assert(loadstring(cases)), env)
else run = assert(load(cases, "shared state cases", "t", env)) end
run()
Test("activation immediately synchronizes without a world-entry event", function()
    local c = Client(); c:Color(0xff0000)
    c:Start(); c:Color(0x0000ff)
end)
Test("unloading stops events, timers and stale callbacks", function()
    local c = Client(); c:Start(); c:Stop()
    local stale, frame = c.timers[#c.timers], c.frame
    c.deactivate()
    c:Color(0)
    Check(not frame.shown and next(frame.events) == nil, "hidden and unregistered")
    Check(frame.scripts.OnEvent == nil and frame.scripts.OnShow == nil, "callbacks removed")
    stale.callback(); c:Advance(5); c:Color(0)
    c.activate(); c:Color(0xff0000)
    Check(c.frame == frame, "reactivation reuses the existing frame")
end)
Test("repeated activation cancels old runtime and reuses texture", function()
    local c = Client(); c:Start(); c:Emit("LOOT_OPENED")
    local stale, texture = c.timers[#c.timers], c.texture
    c:Channel(nil); c.activate(); c:Color(0xff0000)
    stale.callback(); c:Color(0xff0000)
    Check(c.env.aura_env.fishPixel == texture, "same signal texture")
end)
Test("API errors leave black latched until reactivation", function()
    local c = Client(); c.queryError = true
    Check(not pcall(function() c:Emit("UNIT_SPELLCAST_START", "player") end), "error reaches host handler")
    c:Color(0)
    c.queryError = false; c:Emit("BAG_UPDATE_DELAYED"); c:Color(0)
    c.activate(); c:Color(0xff0000)
end)
Test("unsupported required event stays black", function()
    local c = Client({ unsupportedEvent = "UNIT_SPELLCAST_CHANNEL_STOP" })
    c:Color(0); c:Enter(); c:Color(0)
end)
Test("errors after state computation cannot publish a partial result", function()
    local c = Client(); c:Start()
    c.env.C_Timer.NewTimer = function() error("simulated timer failure") end
    Check(not pcall(function() c:Emit("LOOT_OPENED") end), "timer failure reaches host")
    c:Color(0)
    c:Emit("BAG_UPDATE_DELAYED"); c:Color(0)
end)
Test("missing required API stays black", function()
    local c = Client(); c.env.UnitChannelInfo = false
    c:Enter(); c:Color(0)
end)
local tests, assertions = Counts()
print(string.format("ForeverAura: %d state/lifecycle tests, %d assertions passed against the decoded import.", tests, assertions))
