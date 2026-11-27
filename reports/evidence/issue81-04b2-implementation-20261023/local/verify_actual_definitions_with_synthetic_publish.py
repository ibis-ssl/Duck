from pathlib import Path
import importlib.util,json,tempfile
repo=Path("/home/donabe/Duck-issue81-aspire-desktop-bridge")
spec=importlib.util.spec_from_file_location("distribution_definitions",repo/"scripts/tests/verify-linux-distribution-definitions.py")
mod=importlib.util.module_from_spec(spec); spec.loader.exec_module(mod)
with tempfile.TemporaryDirectory(prefix="i81-04b2-publish-fixture-") as temp:
 pub=Path(temp)
 for name,app in mod.APPS.items():
  out=pub/app["output"]; out.mkdir(parents=True)
  for rel in [app["assembly"],app["assembly"].replace(".dll",".deps.json"),app["assembly"].replace(".dll",".runtimeconfig.json")]+app["static"]:
   f=out/rel; f.parent.mkdir(parents=True,exist_ok=True)
   if rel.endswith(".runtimeconfig.json"): f.write_text(json.dumps({"runtimeOptions":{"framework":{"name":app["framework"],"version":"10.0.0"}}}),encoding="utf-8")
   else: f.write_bytes(b"fixture")
 mod.verify_contract(repo,pub)
print("Actual repo lock, AppHost references, three Dockerfiles and synthetic publish outputs pass; no actual .NET publish was run.")
