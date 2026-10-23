import hashlib, pathlib
roots = [
 pathlib.Path("/tmp/issue81-referee-driver-host-design-sync-20261022"),
 pathlib.Path("/tmp/issue81-referee-driver-host-review-preparation-validation-20261022"),
]
selected = [
 "junction.command.json","junction.stdout.log","junction.stderr.log","tool-versions.command.json","tool-versions.stdout.log","tool-versions.stderr.log",
 "cspell-delta.json","sudachi-delta.json","yomiyasu-delta.json","validation-summary.json",
 "final/text.command.json","final/text.stdout.log","final/text.stderr.log","final/targets.command.json","final/targets.stdout.log","final/targets.stderr.log",
 "final/spell.command.json","final/spell.stdout.log","final/spell.stderr.log","final/sudachi-aspire-simulation-test-environment.command.json","final/sudachi-aspire-simulation-test-environment.json","final/sudachi-aspire-simulation-test-environment.stdout.log","final/sudachi-aspire-simulation-test-environment.stderr.log",
 "final/yomiyasu-aspire-simulation-test-environment.command.json","final/yomiyasu-aspire-simulation-test-environment.stdout.log","final/yomiyasu-aspire-simulation-test-environment.stderr.log",
]
review = roots[1]
selected_review = ["full-npm-lint.command.json","full-npm-lint.stdout.log","full-npm-lint.stderr.log","full-sudachi.command.json","full-sudachi.json","full-sudachi.stdout.log","full-sudachi.stderr.log","full-targets.command.json","full-targets.stdout.log","full-targets.stderr.log","validation-summary.json","full-docproof.json"]
items=[]
for rel in selected:
 p=roots[0]/rel
 if p.is_file(): items.append((str(p),hashlib.sha256(p.read_bytes()).hexdigest(),p.stat().st_size))
for rel in selected_review:
 p=review/rel
 if p.is_file(): items.append((str(p),hashlib.sha256(p.read_bytes()).hexdigest(),p.stat().st_size))
out=pathlib.Path("/mnt/c/Users/donabe/AppData/Local/Temp/issue81-04a-evidence-20261022")
(out/"prior-lint-reference.sha256").write_text("".join(f"{sha}  {path}\n" for path,sha,size in items),encoding="utf-8")
(out/"prior-lint-reference.map.txt").write_text("Existing lint evidence was not rerun; original WSL /tmp files remain at the absolute paths listed in prior-lint-reference.sha256.\nSelected files="+str(len(items))+"\nInitial historical direct UNC attempt: not present in these saved source directories; handoff says it reported unsupported and was later replaced by the successful Windows Node + cache junction mirror route. Do not treat it as the definitive lint environment.\n",encoding="utf-8")
print("referenced_files="+str(len(items)))
print("manifest="+str(out/"prior-lint-reference.sha256"))
