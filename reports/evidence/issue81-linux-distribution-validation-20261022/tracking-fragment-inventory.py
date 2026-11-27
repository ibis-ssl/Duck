import hashlib,json,pathlib
base=pathlib.Path("/mnt/c/Users/donabe/AppData/Local/Temp/issue81-04a-lint-20261022/Tracker/Design")
items=[]
for name in ["tasks-status-new-fragment.md","phases-status-new-fragment.md"]:
 p=base/name; b=p.read_bytes(); text=b.decode("utf-8"); items.append({"path":str(p),"bytes":len(b),"lines":len(text.splitlines()),"sha256":hashlib.sha256(b).hexdigest()})
out=pathlib.Path("/mnt/c/Users/donabe/AppData/Local/Temp/issue81-04a-evidence-20261022")
(out/"tracking-fragment-inventory.json").write_text(json.dumps({"filesInspected":len(items),"addedLinesInspected":sum(i["lines"] for i in items),"files":items,"finalChecks":{"textlint":"exit 0 for both files","cspell":"exit 0 for both files","sudachi":"exit 0 for both files"}},ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print((out/"tracking-fragment-inventory.json").read_text())
