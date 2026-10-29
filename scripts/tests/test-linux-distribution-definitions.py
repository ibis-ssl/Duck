#!/usr/bin/env python3
"""Linux配布定義検査器の固定命令と発行出力契約を検証する。"""
from __future__ import annotations
import importlib.util,json,pathlib,tempfile,unittest
p=pathlib.Path(__file__).with_name("verify-linux-distribution-definitions.py")
s=importlib.util.spec_from_file_location("distribution_definitions",p)
assert s and s.loader
m=importlib.util.module_from_spec(s); s.loader.exec_module(m)

class ParserTests(unittest.TestCase):
 def test_parses_digest_copy_and_entrypoint(self):
  parsed=m.parse_dockerfile('FROM image@sha256:'+'a'*64+'\nWORKDIR /app\nCOPY . /app\nENTRYPOINT ["dotnet","App.dll"]\n')
  self.assertEqual(parsed["COPY"],[[".","/app"]]); self.assertEqual(parsed["ENTRYPOINT"],[["dotnet","App.dll"]])
 def test_rejects_bad_entrypoint(self):
  with self.assertRaises(m.ContractError): m.parse_dockerfile("ENTRYPOINT dotnet App.dll\n")
 def test_rejects_unmodeled_instruction(self):
  with self.assertRaises(m.ContractError): m.parse_dockerfile("RUN echo bad\n")

class PublishContractTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory(); self.root=pathlib.Path(self.tmp.name); self.pub=self.root/"publish"; self.pub.mkdir()
  apphost=self.root/"Testing/Duck.Testing.AppHost"; apphost.mkdir(parents=True)
  apphost.joinpath("Program.cs").write_text("\n".join(m.APPHOST_PROGRAM_FRAGMENTS+[m.APPHOST_REGISTRATION_CALL]),encoding="utf-8")
  apphost.joinpath("BaseSimulationResourceRegistration.cs").write_text("\n".join(["internal sealed record BaseSimulationRegistrationOptions(",m.APPHOST_REGISTRATION_OPTION_ORDER,*m.APPHOST_REGISTRATION_FRAGMENTS]),encoding="utf-8")
  images={}
  commits={"framework-simulator":"a52b6bdecd97a277544dd6f275afbf4483781e12","crane":"4063cd31cd5b11b1cc919003907f5f4c527b252d","orion-cm4-sim":"d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a"}
  for key,(ref,digest,media,plats,others) in m.LOCK.items():
   images[key]={"reference":ref,"digest":digest,"mediaType":media,"platforms":[{"os":o,"architecture":a,"digest":d} for o,a,d in plats],"otherDescriptors":[{"os":o,"architecture":a,"digest":d} for o,a,d in others]}
   if key in commits: images[key]["sourceCommit"]=commits[key]
  apphost.joinpath("linux-image-lock.json").write_text(json.dumps({"schemaVersion":1,"images":images}),encoding="utf-8")
  for app in m.APPS.values():
   df=self.root/app["dockerfile"]; df.parent.mkdir(parents=True,exist_ok=True)
   df.write_text(f'FROM {app["base"]}\nWORKDIR /app\nCOPY . /app\nENTRYPOINT ["dotnet","{app["assembly"]}"]\n',encoding="utf-8")
   out=self.pub/app["output"]; out.mkdir()
   for rel in [app["assembly"],app["assembly"].replace(".dll",".deps.json"),app["assembly"].replace(".dll",".runtimeconfig.json")]+app["static"]:
    f=out/rel; f.parent.mkdir(parents=True,exist_ok=True)
    if rel.endswith("runtimeconfig.json"): f.write_text(json.dumps({"runtimeOptions":{"framework":{"name":app["framework"],"version":"10.0.0"}}}),encoding="utf-8")
    else: f.write_bytes(b"fixture")
 def tearDown(self): self.tmp.cleanup()
 def test_real_apphost_defaults_match_the_fixed_catalog(self):
  errors=[]
  m._check_apphost_definitions(p.parents[2],errors)
  self.assertEqual(errors,[])
 def test_rejects_program_default_tag_mutation(self):
  actual=self.root/"Testing/Duck.Testing.AppHost/Program.cs"
  source=actual.read_text(encoding="utf-8")
  actual.write_text(source.replace('Testing:Simulator:ImageTag"] ?? "a52b6bd"','Testing:Simulator:ImageTag"] ?? "latest"'),encoding="utf-8")
  errors=[]
  m._check_apphost_definitions(self.root,errors)
  self.assertTrue(any("Testing:Simulator:ImageTag" in error for error in errors))
 def test_rejects_registration_image_mutation(self):
  actual=self.root/"Testing/Duck.Testing.AppHost/BaseSimulationResourceRegistration.cs"
  source=actual.read_text(encoding="utf-8")
  actual.write_text(source.replace('ghcr.io/ibis-ssl/crane:{options.CraneImageTag}','ghcr.io/ibis-ssl/crane:latest'),encoding="utf-8")
  errors=[]
  m._check_apphost_definitions(self.root,errors)
  self.assertTrue(any("crane:{options.CraneImageTag}" in error for error in errors))
 def test_rejects_misbound_registration_tag(self):
  actual=self.root/"Testing/Duck.Testing.AppHost/Program.cs"
  source=actual.read_text(encoding="utf-8")
  actual.write_text(source.replace("simulatorIbisTeamColor,craneImageTag,cm4SimulatorImageTag,","simulatorIbisTeamColor,cm4SimulatorImageTag,craneImageTag,"),encoding="utf-8")
  errors=[]
  m._check_apphost_definitions(self.root,errors)
  self.assertTrue(any("image tag引数接続" in error for error in errors))
 def test_rejects_disconnected_registration_consumer(self):
  actual=self.root/"Testing/Duck.Testing.AppHost/Program.cs"
  source=actual.read_text(encoding="utf-8")
  actual.write_text(source.replace("BaseSimulationResourceRegistration.Add(","BaseSimulationResourceRegistration.Detached("),encoding="utf-8")
  errors=[]
  m._check_apphost_definitions(self.root,errors)
  self.assertTrue(any("image tag引数接続" in error for error in errors))
 def test_accepts_fixed_definition_and_real_publish_contract(self): m.verify_contract(self.root,self.pub)
 def test_missing_reference_lock_is_reported(self):
  (self.root/"Testing/Duck.Testing.AppHost/linux-image-lock.json").unlink()
  with self.assertRaisesRegex(m.ContractError,"固定参照表がありません"): m.verify_contract(self.root,self.pub)
 def test_missing_dockerfile_is_reported(self):
  (self.root/m.APPS["RuntimeHost"]["dockerfile"]).unlink()
  with self.assertRaisesRegex(m.ContractError,"Dockerfileがありません"): m.verify_contract(self.root,self.pub)
 def test_floating_base_is_rejected(self):
  p=self.root/m.APPS["RuntimeHost"]["dockerfile"]; p.write_text(p.read_text().replace(m.RUNTIME,"mcr.microsoft.com/dotnet/runtime:10.0"),encoding="utf-8")
  with self.assertRaisesRegex(m.ContractError,"FROM"): m.verify_contract(self.root,self.pub)
 def test_referee_driver_requires_aspnet_base(self):
  p=self.root/m.APPS["RefereeDriver.Host"]["dockerfile"]; p.write_text(p.read_text().replace(m.ASPNET,m.RUNTIME),encoding="utf-8")
  with self.assertRaisesRegex(m.ContractError,"FROM"): m.verify_contract(self.root,self.pub)
 def test_copy_must_use_the_actual_publish_context(self):
  p=self.root/m.APPS["DebugHost"]["dockerfile"]; p.write_text(p.read_text().replace("COPY . /app","COPY ./bin /app"),encoding="utf-8")
  with self.assertRaisesRegex(m.ContractError,"COPY"): m.verify_contract(self.root,self.pub)
 def test_apphost_reference_and_lock_tag_must_match(self):
  p=self.root/"Testing/Duck.Testing.AppHost/linux-image-lock.json"; data=json.loads(p.read_text()); data["images"]["sumatra"]["reference"]="tigersmannheim/sumatra:latest"; p.write_text(json.dumps(data),encoding="utf-8")
  with self.assertRaisesRegex(m.ContractError,"sumatra.reference"): m.verify_contract(self.root,self.pub)
 def test_entrypoint_must_match_published_assembly(self):
  p=self.root/m.APPS["DebugHost"]["dockerfile"]; p.write_text(p.read_text().replace("Tracker.DebugHost.dll","Wrong.dll"),encoding="utf-8")
  with self.assertRaisesRegex(m.ContractError,"ENTRYPOINT"): m.verify_contract(self.root,self.pub)
 def test_publish_static_asset_is_required(self):
  (self.pub/m.APPS["DebugHost"]["output"]/"wwwroot/app.css").unlink()
  with self.assertRaisesRegex(m.ContractError,"実発行出力にDocker context用ファイル"): m.verify_contract(self.root,self.pub)
 def test_runtimeconfig_framework_matches_base(self):
  p=self.pub/m.APPS["RefereeDriver.Host"]["output"]/"Duck.Testing.RefereeDriver.Host.runtimeconfig.json"
  p.write_text(json.dumps({"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}),encoding="utf-8")
  with self.assertRaisesRegex(m.ContractError,"runtimeconfigがMicrosoft.AspNetCore.App"): m.verify_contract(self.root,self.pub)
 def test_digest_is_fixed(self):
  p=self.root/"Testing/Duck.Testing.AppHost/linux-image-lock.json"; data=json.loads(p.read_text()); data["images"]["sumatra"]["digest"]="sha256:"+"0"*64; p.write_text(json.dumps(data),encoding="utf-8")
  with self.assertRaisesRegex(m.ContractError,"sumatra.digest"): m.verify_contract(self.root,self.pub)
 def test_unknown_attestation_is_not_a_platform(self):
  p=self.root/"Testing/Duck.Testing.AppHost/linux-image-lock.json"; data=json.loads(p.read_text()); data["images"]["game-controller"]["otherDescriptors"]=[]; p.write_text(json.dumps(data),encoding="utf-8")
  with self.assertRaisesRegex(m.ContractError,"otherDescriptors"): m.verify_contract(self.root,self.pub)
if __name__=="__main__": unittest.main(verbosity=2)
