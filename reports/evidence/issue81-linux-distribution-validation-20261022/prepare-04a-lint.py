import hashlib,json,pathlib,subprocess
repo=pathlib.Path("/home/donabe/Duck-issue81-aspire-desktop-bridge")
mirror=pathlib.Path("/mnt/c/Users/donabe/AppData/Local/Temp/issue81-04a-lint-20261022")
evidence=pathlib.Path("/mnt/c/Users/donabe/AppData/Local/Temp/issue81-04a-evidence-20261022")
rel="Tracker/Design/Testing/aspire-simulation-test-environment.md"
baseline="4e3099e75c4afab8ba1f11c67500ebe29f17fed3"
current="fdff6ec8e5851a4a7d1d20d0dad4f69503b926a9"
def gitblob(ref,path): return subprocess.run(["git","show",ref+":"+path],cwd=repo,check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE).stdout
base=gitblob(baseline,rel); curr=gitblob(current,rel)
basepath=mirror/"baseline"/rel; basepath.parent.mkdir(parents=True,exist_ok=True); basepath.write_bytes(base)
diff=subprocess.run(["git","diff","--unified=0",baseline,current,"--",rel],cwd=repo,check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
(evidence/"04a-design-baseline-current.diff").write_bytes(diff.stdout)
lines=[x[1:] for x in diff.stdout.decode("utf-8").splitlines() if x.startswith("+") and not x.startswith("+++")]
fragment=mirror/"fragments"/(pathlib.Path(rel).stem+"-added-lines.md"); fragment.parent.mkdir(parents=True,exist_ok=True); fragment.write_text("\n".join(lines)+"\n",encoding="utf-8")
context={"baselineRef":baseline,"currentRef":current,"sourcePath":rel,"baselineBytes":len(base),"baselineSha256":hashlib.sha256(base).hexdigest(),"currentBytes":len(curr),"currentSha256":hashlib.sha256(curr).hexdigest(),"mirrorBytes":(mirror/rel).stat().st_size,"mirrorSha256":hashlib.sha256((mirror/rel).read_bytes()).hexdigest(),"diffAddedLines":len(lines),"addedLineFragmentPath":str(fragment),"addedLineFragmentSha256":hashlib.sha256(fragment.read_bytes()).hexdigest(),"addedLineFragmentBytes":fragment.stat().st_size,"textlintConfigSha256":hashlib.sha256((mirror/".textlintrc.json").read_bytes()).hexdigest(),"cspellConfigSha256":hashlib.sha256((mirror/"cspell.config.jsonc").read_bytes()).hexdigest(),"packageJsonSha256":hashlib.sha256((mirror/"package.json").read_bytes()).hexdigest(),"sudachiWhitelistSha256":hashlib.sha256((mirror/"tools/lint/markdown-whitelist.yaml").read_bytes()).hexdigest(),"sudachiCheckerSha256":hashlib.sha256((repo/"tools/lint/scripts/check-markdown-whitelist-sudachi.py").read_bytes()).hexdigest(),"note":"No source/config/dictionary changes were made; baseline and current targets use existing Temp mirror and existing node_modules junction/cache."}
(evidence/"04a-lint-source-context.json").write_text(json.dumps(context,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print(json.dumps(context,ensure_ascii=False,indent=2))
