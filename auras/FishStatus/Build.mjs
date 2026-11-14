// Run from the repository root: node auras/FishStatus/Build.mjs
import fs from 'node:fs';
import { createRequire } from 'node:module';
import path from 'node:path';
import zlib from 'node:zlib';
const require = createRequire(path.resolve('artifacts/lua-test-runtime/package.json'));
const { lua, lauxlib, lualib, to_luastring, to_jsstring } = require('fengari');
const state = lauxlib.luaL_newstate();
lualib.luaL_openlibs(state);
function execute(source, name) {
  const bytes = to_luastring(source);
  if (lauxlib.luaL_loadbuffer(state, bytes, bytes.length, to_luastring(name)) ||
      lua.lua_pcall(state, 0, 0, 0)) {
    throw new Error(to_jsstring(lua.lua_tostring(state, -1)));
  }
}
function globalString(name, value) {
  lua.lua_pushstring(state, to_luastring(value));
  lua.lua_setglobal(state, to_luastring(name));
}
execute('strmatch = string.match; arg = {}', 'WoW string alias');
for (const library of ['LibStub', 'LibSerialize', 'LibDeflate']) {
  let source = fs.readFileSync(`artifacts/aura-tools/${library}.lua`, 'utf8');
  // WoW Lua uses doubles; Fengari's integer arithmetic otherwise overflows at 2^31.
  // Preserve comments and strings, and load integer literals as doubles in these libraries.
  source = source.replace(/--\[(=*)\[[\s\S]*?\]\1\]|--[^\n]*|\[(=*)\[[\s\S]*?\]\2\]|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|\b(?:0x[\da-fA-F]+|\d+(?:\.\d*)?(?:[eE][+-]?\d+)?)\b/g,
    token => /^\d+$/.test(token) ? `${token}.0` : token);
  execute(source, library);
}
globalString('FishAuraSource', fs.readFileSync('auras/FishStatus/OnShow.lua', 'utf8').replaceAll('\r\n', '\n'));
execute(fs.readFileSync('auras/FishStatus/Build.lua', 'utf8'), 'Build.lua');
lua.lua_getglobal(state, to_luastring('FishAuraSerialized'));
const serialized = Buffer.from(lua.lua_tolstring(state, -1));
lua.lua_pop(state, 1);
const compressed = zlib.deflateRawSync(serialized, { level: 9 });
lua.lua_getglobal(state, to_luastring('FishAuraEncode'));
lua.lua_pushlstring(state, compressed, compressed.length);
if (lua.lua_pcall(state, 1, 0, 0)) throw new Error(to_jsstring(lua.lua_tostring(state, -1)));
globalString('FishHelperTestSource', fs.readFileSync('Tests/FishHelper.Tests.lua', 'utf8').replaceAll('\r\n', '\n'));
execute(fs.readFileSync('Tests/ForeverAura.Tests.lua', 'utf8'), 'ForeverAura.Tests.lua');
lua.lua_getglobal(state, to_luastring('FishAuraImport'));
fs.writeFileSync('auras/FishStatus/Import.txt', to_jsstring(lua.lua_tostring(state, -1)));
lua.lua_close(state);
