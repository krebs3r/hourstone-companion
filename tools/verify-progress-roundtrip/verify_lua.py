"""Execute actual generated Data.lua with actual addon code in Lua 5.1; WoW APIs are mocks."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

parser = argparse.ArgumentParser()
parser.add_argument("--addon-root", required=True)
parser.add_argument("--output", required=True)
args = parser.parse_args()
addon = Path(args.addon_root).resolve()
output = Path(args.output).resolve()
sys.path.insert(0, str(addon / "tests"))
from run import runtime
from lupa.lua51 import LuaRuntime

def plain(value):
    if not hasattr(value, "items"):
        return value
    items = dict(value.items())
    if items and set(items) == set(range(1, len(items) + 1)):
        return [plain(items[i]) for i in range(1, len(items) + 1)]
    return {key: plain(item) for key, item in items.items()}

def legacy_runtime():
    # v0.3.1 is read from the existing repository tag, without checkout or repository mutation.
    def source(name):
        return subprocess.check_output(["git", "-C", str(addon), "show", "v0.3.1:Hourstone/" + name]).decode("utf-8")
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute('TEST_LOCALE="deDE"; TEST_VERSION="0.3.1"; WOW_PROJECT_ID=1; BackdropTemplateMixin={}')
    lua.execute((addon / "tests/wow_mock.lua").read_text(encoding="utf-8"))
    lua.execute("H={}")
    loader = lua.eval('function(code,name) local f,e=loadstring(code,name); assert(f,e); f("Hourstone",H) end')
    for line in source("Hourstone.toc").splitlines():
        if line.endswith(".lua"):
            loader(source(line), line)
    return lua

manifest = json.loads((output / "companion-report.json").read_text(encoding="utf-8"))
results = []
for case in manifest["cases"]:
    saved = Path(case["directory"], "SavedVariables.lua").read_text(encoding="utf-8")
    data_bytes = Path(case["directory"], "Data.lua").read_bytes()
    data = data_bytes.decode("utf-8")
    baseline = legacy_runtime() if case["legacy"] else runtime()
    baseline.execute(saved)
    baseline.execute("DB=assert(H.M.Init(HourstoneDB)); H.P:Init(DB)")
    local_before = plain(baseline.globals().DB)
    lua = legacy_runtime() if case["legacy"] else runtime()
    lua.execute(saved)
    lua.execute(data)  # Exact Companion writer output, not a reconstructed JSON scope.
    lua.globals().NOW_TEST = case["now"]
    lua.globals().TARGET_SOURCE = case["sourceId"]
    lua.execute('''DB=assert(H.M.Init(HourstoneDB))
        assert(DB.sourceId==TARGET_SOURCE)
        assert(H.S.Import(DB,HourstoneSync)); assert(H.S.status=="ready")
        H.P:Init(DB); H.P.now=NOW_TEST
        DISPLAY=H.S.Display(DB); local count=0
        for _,char in pairs(DISPLAY.characters) do count=count+1; CHAR=char end
        assert(count==1); RESULT=H.P:Get(CHAR)''')
    assert plain(lua.globals().DB)["characters"] == local_before["characters"], case["name"] + ": foreign played data entered SavedVariables"
    assert plain(lua.globals().DB)["progress"] == local_before["progress"], case["name"] + ": foreign progress entered local cache"
    assert lua.globals().CHAR.seconds == case["seconds"], case["name"] + ": wrong played winner"
    actual = plain(lua.globals().RESULT)
    expected = case["expected"]
    for family in ("keystone", "weekly"):
        got = {k: v for k, v in actual[family].items() if k not in ("status", "expired")}
        assert got == expected[family], (case["name"], family, got, expected[family])
        state = "known" if expected[family]["updatedAt"] <= case["now"] < expected[family]["resetAt"] else "stale"
        assert actual[family]["status"] == state, (case["name"], family, "state")
    for family in ("dungeon", "raid", "world"):
        got = {k: v for k, v in actual["vault"]["rows"][family].items() if k not in ("status", "expired")}
        assert got == expected["vault"]["rows"][family], (case["name"], family, got)
        row = expected["vault"]["rows"][family]
        state = "known" if row["updatedAt"] <= case["now"] < row["resetAt"] else "stale"
        assert actual["vault"]["rows"][family]["status"] == state, (case["name"], family, "state")
    results.append({"name": case["name"], "passed": True, "addon": "0.3.1 tag" if case["legacy"] else "working tree 0.3.2",
                    "dataSha256": hashlib.sha256(data_bytes).hexdigest(), "formatVersion": lua.globals().HourstoneSync.formatVersion,
                    "localCacheUnchanged": True, "actual": actual})
    print("PASS actual Lua importer:", case["name"])
report = {"passed": True, "scope": manifest["scope"], "companionChecks": manifest["checks"], "luaCheckpoints": results,
          "limitations": ["Synthetic SavedVariables, not live game capture.", "Two local profiles, not two physical PCs.", "File copies, not a cloud-provider network test.", "Lua 5.1 with mocked WoW APIs, not native WoW execution."]}
(output / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"PASS complete simulated roundtrip: {len(results)} actual generated Lua files; {output / 'report.json'}")
