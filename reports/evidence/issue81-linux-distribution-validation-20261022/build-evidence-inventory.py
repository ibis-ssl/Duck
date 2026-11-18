import hashlib,json,pathlib
root=pathlib.Path("/mnt/c/Users/donabe/AppData/Local/Temp/issue81-04a-evidence-20261022")
manifest=root/"evidence-files.sha256"
summary=root/"evidence-inventory.json"
entries=[]
for p in sorted(x for x in root.rglob("*") if x.is_file() and x not in (manifest,summary)):
 b=p.read_bytes(); rel=p.relative_to(root).as_posix(); entries.append({"path":rel,"bytes":len(b),"sha256":hashlib.sha256(b).hexdigest()})
manifest.write_text("".join(f"{e['sha256']}  {e['path']}\n" for e in entries),encoding="utf-8")
by_top={}
for e in entries:
 top=e["path"].split("/",1)[0]; rec=by_top.setdefault(top,{"files":0,"bytes":0}); rec["files"]+=1; rec["bytes"]+=e["bytes"]
zip_files=[e for e in entries if e["path"].lower().endswith(".zip")]
pk_magic=[]; gzip_files=[]
for e in entries:
 p=root/e["path"]; head=p.open("rb").read(4)
 if head.startswith(b"PK\x03\x04"): pk_magic.append(e["path"])
 if e["path"].lower().endswith(".gz"): gzip_files.append({"path":e["path"],"magic":head.hex(" "),"bytes":e["bytes"],"sha256":e["sha256"]})
totals={"files":len(entries),"bytes":sum(e["bytes"] for e in entries)}
obj={"root":"C:\\Users\\donabe\\AppData\\Local\\Temp\\issue81-04a-evidence-20261022","manifest":"evidence-files.sha256","manifestSha256":hashlib.sha256(manifest.read_bytes()).hexdigest(),"manifestEntries":len(entries),"totals":totals,"byTopLevel":by_top,"zipExtensionFiles":zip_files,"PK0304MagicFiles":pk_magic,"gzipFiles":gzip_files,"notes":"Manifest excludes itself and this summary to avoid self-reference. Raw source archive is retained in Temp; duplicate extracted sources and dotnet-test.binlog were excluded per storage-map.txt."}
summary.write_text(json.dumps(obj,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
print(json.dumps(obj,ensure_ascii=False,indent=2))
